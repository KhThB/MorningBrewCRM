"""Additional v1 contract checks, called by integration.py on its disposable database only."""
import concurrent.futures
import json
import re
import subprocess
import tempfile
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request
import uuid


def verify(base, owner, manager, customer, server, database, passed):
    assert any(word in database.lower() for word in ('test', 'verify'))

    def request(path, token=None, body=None, method=None, status=200, headers=None, raw=None):
        metadata = {'Content-Type': 'application/json', **(headers or {})}
        if token:
            metadata['Authorization'] = 'Bearer ' + token
        data = raw if raw is not None else None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(base + path, data=data, headers=metadata,
                                     method=method or ('POST' if data is not None else 'GET'))
        try:
            response = urllib.request.urlopen(req, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            code, result_headers, text = response.code, response.headers, response.read()
        assert code == status, (path, code, text.decode()[:500])
        result = json.loads(text) if text else None
        if code >= 400:
            assert result['error']['code'] == code
            assert isinstance(result['error']['details'], list)
        if path.startswith('/v1/'):
            assert result_headers['Cache-Control'] == 'no-store'
        return result, result_headers

    def sql(command):
        with tempfile.NamedTemporaryFile(mode='w',suffix='.sql',encoding='utf-8',delete=False) as file:
            file.write(command)
            path = Path(file.name)
        try:
            result = subprocess.run(['sqlcmd', '-S', server, '-d', database, '-E', '-C', '-I', '-b',
                                     '-f', '65001', '-i', str(path)], capture_output=True, text=True,
                                    encoding='utf-8', errors='replace', timeout=40)
        finally:
            path.unlink()
        assert result.returncode == 0, result.stdout + result.stderr

    # Protocol errors must be indistinguishable in shape regardless of which layer rejects them.
    body, headers = request('/v1/customers', status=401)
    assert headers['WWW-Authenticate'] == 'Bearer'
    request('/api/admin/customers', owner, status=404)
    request('/v1/missing-resource', owner, status=404)
    request('/v1/auth/login', body={}, status=400)
    request('/v1/auth/login', raw=b'{', status=400)
    request('/v1/auth/login', method='DELETE', status=405)
    request('/v1/auth/login', raw=b'hello', headers={'Content-Type': 'text/plain'}, status=415)
    request('/v1/products/1/active', owner, {}, method='PUT', status=400)
    request('/v1/products/9999999/active', owner, {'is_active': False}, method='PUT', status=404)
    request('/v1/products/1/active', owner, {'isActive': False}, method='PUT', status=400)
    passed('V1 uniform 400/401/404/405/415 errors; strict field names and required boolean')

    # Cursor must survive changes ahead of it; copying it to a different identity/filter fails.
    first, _ = request('/v1/customers?pageSize=1', owner)
    assert len(first['items']) == 1 and first['next_page_token']
    cursor = urllib.parse.quote(first['next_page_token'], safe='')
    new_account, created_headers = request('/v1/accounts', owner, {
        'email': 'cursor.' + uuid.uuid4().hex + '@example.test', 'full_name': 'Cursor Test',
        'password': 'Integration!2026', 'role': 'Customer'
    }, status=201)
    assert 'password' not in json.dumps(new_account).lower()
    assert new_account['name'] == 'accounts/' + str(new_account['user_id'])
    assert new_account['create_time'].endswith('Z')
    fetched, _ = request(created_headers['Location'], owner)
    assert fetched == new_account
    second, _ = request('/v1/customers?pageSize=1&pageToken=' + cursor, owner)
    assert second['items'][0]['customer_id'] < first['items'][0]['customer_id']
    request('/v1/customers?pageToken=' + cursor, manager, status=400)
    request('/v1/customers?q=changed&pageToken=' + cursor, owner, status=400)
    request('/v1/accounts?pageToken=' + cursor, owner, status=400)
    request('/v1/customers?pageToken=broken', owner, status=400)
    for size in ('0', '-1', '101', 'invalid'):
        request('/v1/customers?pageSize=' + size, owner, status=400)
    # Exercise string/composite and numeric keyset cursors until exhaustion.
    for collection, keys in [('permissions', ('role', 'permission')), ('staff', ('branch_id', 'user_id', 'role')),
                             ('variants', ('variant_id',))]:
        token = ''
        seen = set()
        for _ in range(200):
            page, _ = request(f'/v1/{collection}?pageSize=2&pageToken=' + urllib.parse.quote(token, safe=''), owner)
            for item in page['items']:
                identity = tuple(item[key] for key in keys)
                assert identity not in seen, (collection, identity)
                seen.add(identity)
            token = page['next_page_token']
            if not token:
                break
        else:
            raise AssertionError('Pagination did not terminate')
        assert seen
    passed('Encrypted keyset pagination: stable inserts, actor/filter binding, composite keys and bounds')

    # PATCH must preserve omitted fields, allow explicit null, and reject stale concurrent writers.
    profile, headers = request('/v1/me/profile', customer)
    assert profile['etag'] == headers['ETag'] and profile['create_time'].endswith('Z')
    assert profile['date_of_birth'] is None or re.fullmatch(r'\d{4}-\d{2}-\d{2}', profile['date_of_birth'])
    request('/v1/me/profile', customer, {'phone': '0901234567'}, method='PATCH', status=428)
    request('/v1/me/profile', customer, {'loyalty_points': 100000}, method='PATCH',
            headers={'If-Match': profile['etag']}, status=400)
    request('/v1/me/profile', customer, {'full_name': None}, method='PATCH',
            headers={'If-Match': profile['etag']}, status=400)
    request('/v1/me/profile', customer, {'date_of_birth': 'not-a-date'}, method='PATCH',
            headers={'If-Match': profile['etag']}, status=400)
    updated, _ = request('/v1/me/profile', customer, {'phone': None}, method='PATCH',
                         headers={'If-Match': profile['etag']})
    assert updated['phone'] is None and updated['full_name'] == profile['full_name']
    assert updated['loyalty_points'] == profile['loyalty_points']
    request('/v1/me/profile', customer, {'phone': '0901234567'}, method='PATCH',
            headers={'If-Match': profile['etag']}, status=412)
    def race(number):
        req = urllib.request.Request(base + '/v1/me/profile', method='PATCH',
            data=json.dumps({'phone': number}).encode(), headers={'Authorization': 'Bearer ' + customer,
            'If-Match': updated['etag'], 'Content-Type': 'application/json'})
        try:
            with urllib.request.urlopen(req, timeout=30) as response:
                return response.status
        except urllib.error.HTTPError as error:
            with error:
                assert json.loads(error.read())['error']['status'] == 'FAILED_PRECONDITION'
            return error.code
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        assert sorted(pool.map(race, ['0901111222', '0903333444'])) == [200, 412]
    passed('PATCH preserves omitted fields, supports null, blocks mass assignment and concurrent overwrites')

    # Global prices affect future orders, while immutable historical financial snapshots stay intact.
    request('/v1/variants/1/price', manager, {'price': 42000}, method='PUT', status=403)
    request('/v1/variants/1/price', owner, {}, method='PUT', status=400)
    request('/v1/variants/1/price', owner, {'price': -1}, method='PUT', status=400)
    request('/v1/variants/1/price', owner, {'price': 42000, 'branch_id': 1}, method='PUT', status=400)
    request('/v1/variants/9999999/price', owner, {'price': 42000}, method='PUT', status=404)
    request('/v1/variants/1/price', owner, {'price': 42000}, method='PUT')
    current, _ = request('/v1/variants/1/price', owner)
    assert current['price'] == 42000
    sql("""
        SET NOCOUNT ON;
        IF EXISTS(SELECT 1 FROM dbo.BranchProductVariants WHERE VariantID=1 AND SellingPrice<>42000)
            THROW 52000,'Variant prices not aligned.',1;
        -- Simulate leftover legacy overrides; core procedures must ignore their amounts.
        UPDATE dbo.BranchProductVariants SET SellingPrice=99999 WHERE VariantID=1;
        UPDATE dbo.ModifierOptions SET ExtraPrice=7000 WHERE ModifierOptionID=5;
        UPDATE dbo.BranchModifierOptions SET ExtraPrice=99999 WHERE ModifierOptionID=5;
        DECLARE @orders TABLE(ID int);
        DECLARE @key uniqueidentifier=NEWID(),@id int;
        INSERT @orders EXEC dbo.CreateOrder @ActorID=5,@BranchID=1,@Source='App',@Fulfillment='Pickup',
            @PaymentMode='Prepaid',@CustomerID=1,@RequestKey=@key,
            @Items=N'[{"variantId":1,"quantity":1,"optionIds":[1,3,5]}]';
        SELECT @id=ID FROM @orders;
        IF NOT EXISTS(SELECT 1 FROM dbo.OrderDetails WHERE OrderID=@id AND UnitPrice=42000)
            THROW 52000,'Order ignored shared variant price.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.OrderItemModifiers m JOIN dbo.OrderDetails d ON d.OrderDetailID=m.OrderDetailID
                      WHERE d.OrderID=@id AND m.ModifierOptionID=5 AND m.ExtraPrice=7000)
            THROW 52000,'Order ignored shared modifier price.',1;
        IF NOT EXISTS(SELECT 1 FROM dbo.OrderDetails WHERE OrderID=1 AND UnitPrice=30000)
            THROW 52000,'Historical snapshot changed.',1;
        """)
    # Restore legacy rows to the global value for easier manual inspection of the test database.
    request('/v1/variants/1/price', owner, {'price': 42000}, method='PUT')
    passed('Owner-only shared pricing; legacy overrides ignored and historical prices preserved')

    spec, _ = request('/swagger/v1/swagger.json')
    assert spec['components']['securitySchemes']['Bearer']['scheme'] == 'bearer'
    assert '/v1/customers' in spec['paths'] and '/v1/variants/{id}/price' in spec['paths']
    assert '201' in spec['paths']['/v1/accounts']['post']['responses']
    assert '400' in spec['paths']['/v1/auth/login']['post']['responses']
    assert spec['paths']['/v1/auth/login']['post'].get('security', []) == []
    assert not any(path.startswith('/api/') for path in spec['paths'])
    assert any('full_name' in schema.get('properties', {}) for schema in spec['components']['schemas'].values())
    passed('Swagger documents v1 routes, snake_case DTOs, bearer login, 201 and error contracts')

    _, cors = request('/v1/me/profile', method='OPTIONS', status=204, headers={
        'Origin': 'http://localhost:7287', 'Access-Control-Request-Method': 'PATCH',
        'Access-Control-Request-Headers': 'authorization,if-match,content-type'})
    assert cors['Access-Control-Allow-Origin'] == 'http://localhost:7287'
    _, rejected = request('/v1/me/profile', method='OPTIONS', status=204, headers={
        'Origin': 'https://untrusted.example', 'Access-Control-Request-Method': 'PATCH'})
    assert rejected.get('Access-Control-Allow-Origin') is None
    for _ in range(25):
        req = urllib.request.Request(base + '/v1/auth/login', data=b'{}', headers={'Content-Type':'application/json'})
        try:
            urllib.request.urlopen(req, timeout=10).close()
        except urllib.error.HTTPError as error:
            with error:
                body = json.loads(error.read())
                assert body['error']['code'] == error.code
                if error.code == 429:
                    assert body['error']['status'] == 'RESOURCE_EXHAUSTED' and error.headers['Retry-After']
                    break
                assert error.code == 400
    else:
        raise AssertionError('Login rate limit did not reject requests')
    passed('CORS allowlist and preflight; login rate limit uses error envelope and Retry-After')

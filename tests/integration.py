"""Run against a NEW seeded database with Test/Verify in its name. Never uses the live database.
Requires .NET build output and Python standard library. Processes started here are always stopped.
"""
import argparse, concurrent.futures, datetime, html, http.cookiejar, json, os, re, subprocess, time, urllib.request, urllib.error, uuid
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--database',required=True)
p.add_argument('--server',default=r'(localdb)\MSSQLLocalDB')
a=p.parse_args()
if not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]+',a.database) or not any(x in a.database.lower() for x in ('test','verify')):
    raise SystemExit('Only disposable Test/Verify databases are allowed.')
root=Path(__file__).resolve().parents[1]
logs=root/'database'/'qa';logs.mkdir(exist_ok=True)
api='http://127.0.0.1:15029';admin='http://127.0.0.1:15191'
processes=[];files=[];checks=[]
def start(project,url,extra):
    env=os.environ.copy();env.update({'ASPNETCORE_ENVIRONMENT':'Development','ASPNETCORE_URLS':url,**extra})
    log=open(logs/(project+'.log'),'w',encoding='utf-8');files.append(log)
    process=subprocess.Popen(['dotnet',str(root/project/'bin/Debug/net10.0'/f'{project}.dll')],cwd=root/project,env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=getattr(subprocess,'CREATE_NO_WINDOW',0))
    processes.append(process)
    for _ in range(60):
        if process.poll() is not None: raise AssertionError(f'{project} stopped. See {log.name}')
        try: urllib.request.urlopen(url+('/health' if project.endswith('Api') else '/Login'),timeout=1);return
        except (OSError,urllib.error.URLError): time.sleep(.5)
    raise AssertionError(f'{project} did not start.')
def call(path,token=None,body=None,method=None,status=200):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    request=urllib.request.Request(api+path,data=None if body is None else json.dumps(body).encode(),headers=headers,method=method or ('POST' if body is not None else 'GET'))
    try:
        with urllib.request.urlopen(request,timeout=35) as response: code=response.status;data=response.read()
    except urllib.error.HTTPError as e:code=e.code;data=e.read()
    assert code==status,(path,code,data.decode()[:600])
    return json.loads(data) if data else None
def passed(name):
    checks.append(name);print('PASS',name,flush=True)
def login(email,password='MorningBrew!2026'):
    return call('/api/auth/login',body={'username':email,'password':password})['token']
def browser():
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def get_page(b,path):
    with b.open(admin+path,timeout=35) as response:return response.read().decode(),response.url
def post_page(b,path,form):
    page,_=get_page(b,path)
    match=re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"',page)
    assert match,('missing CSRF token',path)
    form={**form,'__RequestVerificationToken':html.unescape(match[1])}
    with b.open(admin+path,urllib.parse.urlencode(form).encode(),timeout=35) as response:return response.read().decode(),response.url
try:
    start('MorningBrew.Api',api,{'ConnectionStrings__MorningBrew':f'Server={a.server};Database={a.database};Integrated Security=true;TrustServerCertificate=true'})
    start('MorningBrew.Admin',admin,{'Api__BaseUrl':api+'/'})
    call('/api/admin/customers',status=401)
    call('/api/auth/login',body={'username':'owner@morningbrew.test','password':'wrong'},status=401)
    owner=login('owner@morningbrew.test');manager=login('manager.1@morningbrew.test');west=login('area.west@morningbrew.test')
    staff=login('staff.1@morningbrew.test');customer=login('lan@morningbrew.test')
    passed('Seeded PasswordHasher accounts and anonymous rejection')
    call('/api/admin/customers',customer,status=403);call('/api/admin/customers',staff,status=403)
    call('/api/admin/accounts',manager,status=403)
    assert all(x['BranchID']==3 for x in call('/api/auth/me',west)['branches'])
    for section in ('customers','feedbacks','surveys','promotions','products','accounts','permissions','staff','shifts','suppliers','dashboard/overview','reports/revenue','lookups/options'):
        call('/api/admin/'+section,owner)
    passed('Management reads and role boundaries')
    supplier={'code':uuid.uuid4().hex[:20],'name':'Integration Supplier','phone':'0901111222','email':'supplier@example.test','productGroup':'Coffee'}
    call('/api/admin/suppliers',manager,supplier,status=403)
    supplier_id=call('/api/admin/suppliers',owner,supplier)[0]['SupplierID']
    call(f'/api/admin/suppliers/{supplier_id}/active',owner,{'isActive':False},method='PUT',status=204)
    future=datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=3)
    shift={'userId':4,'branchId':1,'startsAt':future.isoformat(),'endsAt':(future+datetime.timedelta(hours=4)).isoformat()}
    call('/api/admin/shifts',west,shift,status=403)
    shift_id=call('/api/admin/shifts',manager,shift)[0]['ShiftID']
    call('/api/admin/shifts',manager,shift,status=400)
    call(f'/api/admin/shifts/{shift_id}/cancel',manager,{},status=204)
    call('/api/admin/permissions',owner,{'role':'BranchManager','code':'Surveys.Manage','allowed':False},method='PUT',status=204)
    assert call('/api/admin/surveys',manager)==[]
    call('/api/admin/permissions',owner,{'role':'BranchManager','code':'Surveys.Manage','allowed':True},method='PUT',status=204)
    passed('Suppliers, shift scope/overlap protection and live role permissions')
    profile=call('/api/customer/profile',customer);assert profile['CustomerID']==1
    feedback=call('/api/customer/feedbacks',customer,{'content':'Integration feedback','type':'Service','rating':5,'branchId':1})[0]['FeedbackID']
    call(f'/api/admin/feedbacks/{feedback}/reply',west,{'status':'Resolved','responseContent':'not allowed'},status=403)
    call(f'/api/admin/feedbacks/{feedback}/reply',manager,{'status':'Resolved','responseContent':'Thank you'},status=204)
    assert call('/api/customer/feedbacks',customer)[0]['ResponseContent']=='Thank you'
    passed('Feedback submit/reply, same SQL data and cross-area denial')
    before=call('/api/customer/profile',customer)['LoyaltyPoints'];key=str(uuid.uuid4())
    adjustment={'delta':100,'note':'Integration correction','requestKey':key}
    call('/api/admin/customers/1/points',manager,adjustment,status=403)
    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:list(pool.map(lambda _:call('/api/admin/customers/1/points',owner,adjustment,status=204),range(2)))
    assert call('/api/customer/profile',customer)['LoyaltyPoints']==before+100
    call('/api/admin/customers/1/points',owner,{**adjustment,'delta':101},status=400)
    passed('Concurrent point retries apply once; changed payload rejected')
    now=datetime.datetime.now(datetime.timezone.utc)
    scope={'scope':'Chain','areaId':None,'branchId':None,'startsAt':(now-datetime.timedelta(minutes=1)).isoformat(),'endsAt':(now+datetime.timedelta(days=7)).isoformat()}
    promotion=call('/api/admin/promotions',owner,{**scope,'name':'Integration voucher','description':'Test','discountType':'Amount','discountValue':5000,'maxDiscount':None,'minSubtotal':10000,'pointsCost':10,'totalLimit':50,'perCustomerLimit':2})[0]['PromotionID']
    redemption={'requestKey':str(uuid.uuid4())}
    call(f'/api/customer/promotions/{promotion}/redeem',customer,redemption)
    call(f'/api/customer/promotions/{promotion}/redeem',customer,redemption)
    assert call('/api/customer/profile',customer)['LoyaltyPoints']==before+90
    passed('Voucher redemption and idempotent point deduction')
    survey_input={**scope,'title':'Integration survey','description':'Test','questions':[{'text':'How was it?','type':'Rating','isRequired':True,'ratingMin':1,'ratingMax':5},{'text':'Drink?','type':'SingleChoice','isRequired':True,'options':['Coffee','Tea']}]}
    call('/api/admin/surveys',manager,survey_input,status=403)
    survey=call('/api/admin/surveys',owner,survey_input)[0]['SurveyID']
    call(f'/api/admin/surveys/{survey}/publish',owner,{},status=204)
    call(f'/api/admin/surveys/{survey}/invite',owner,{'customerId':1},status=204)
    questions=call(f'/api/customer/surveys/{survey}',customer)
    call(f'/api/customer/surveys/{survey}/submit',customer,{'answers':[]},status=400)
    answers={'answers':[{'questionId':questions['questions'][0]['QuestionID'],'rating':5},{'questionId':questions['questions'][1]['QuestionID'],'optionIds':[questions['options'][0]['OptionID']]}]}
    call(f'/api/customer/surveys/{survey}/submit',customer,answers,status=204)
    call(f'/api/customer/surveys/{survey}/submit',customer,answers,status=400)
    assert len(call(f'/api/admin/surveys/{survey}/results',owner))==2
    passed('Survey creation, scope, publish, invitation, typed answers and duplicate rejection')
    email='integration.'+uuid.uuid4().hex[:10]+'@morningbrew.test'
    uid=call('/api/admin/accounts',owner,{'email':email,'fullName':'Integration Customer','password':'Integration!2026','phone':'0908888777','role':'Customer'})[0]['UserID']
    member=login(email,'Integration!2026')
    cid=call('/api/customer/profile',member)['CustomerID']
    assert cid!=uid
    assert not any(x['CustomerID']==cid for x in call('/api/admin/customers',manager))
    call('/api/admin/accounts/'+str(uid)+'/active',owner,{'isActive':False},method='PUT',status=204)
    call('/api/customer/profile',member,status=401)
    passed('New account/profile creation and immediate session revocation')
    b=browser()
    page,url=post_page(b,'/Login',{'Email':'owner@morningbrew.test','Password':'MorningBrew!2026'})
    assert '/Login' not in url and 'Tổng quan' in page
    for section in ('customers','feedbacks','surveys','promotions','products','accounts','permissions','staff','reports','shifts','suppliers'):
        page,url=get_page(b,'/Workspace/'+section)
        assert 'Không thể tải trang' not in page and 'Không thể xử lý yêu cầu' not in page,(section,page[:300])
    passed('Admin cookie login and all management pages render')
    try:b.open(admin+'/Workspace/accounts',urllib.parse.urlencode({'operation':'account-active','id':uid,'active':'true'}).encode())
    except urllib.error.HTTPError as e:assert e.code==400
    else:raise AssertionError('CSRF protection missing')
    call('/api/auth/login',body={'username':email,'password':'Integration!2026'},status=401)
    page,_=post_page(b,'/Workspace/accounts',{'operation':'account-active','id':uid,'active':'true'})
    assert 'Đã lưu thay đổi' in html.unescape(page), html.unescape(page)[-2500:]
    member=login(email,'Integration!2026')
    assert call('/api/customer/profile',member)['CustomerID']==cid
    passed('Razor form CSRF enforcement and Admin-to-API-to-SQL write')
    cb=browser();page,url=post_page(cb,'/Login',{'Email':'lan@morningbrew.test','Password':'MorningBrew!2026'})
    assert '/Portal' in url
    for section in ('profile','loyalty','wallet','promotions','feedbacks','surveys'):
        page,url=get_page(cb,'/Portal/'+section)
        assert 'Không thể tải trang' not in page
    passed('Customer portal renders shared SQL data')
    call('/api/auth/logout',owner,{},status=204)
    call('/api/admin/customers',owner,status=401)
    passed('Logout invalidates API session')
    processes[0].terminate();processes[0].wait(timeout=10)
    start('MorningBrew.Api',api,{'ConnectionStrings__MorningBrew':f'Server={a.server};Database={a.database};Integrated Security=true;TrustServerCertificate=true'})
    assert call('/api/customer/profile',member)['CustomerID']==cid
    assert call('/api/customer/profile',customer)['LoyaltyPoints']==before+90
    passed('Accounts, sessions and points persist across API restart')
finally:
    for process in reversed(processes):
        process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait()
    for f in files:f.close()
    (logs/'api-integration-results.json').write_text(json.dumps({'database':a.database,'passed':checks},indent=2,ensure_ascii=False),encoding='utf-8')
print(f'All {len(checks)} integration groups passed.')

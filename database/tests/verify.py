"""Integration/concurrency tests against a fresh database installed with demo data.
Run ONLY on a disposable verification database; these scenarios intentionally change demo records.
Uses installed sqlcmd and Python standard library. No database is deleted by this runner.
"""
import argparse, concurrent.futures, json, subprocess, time, uuid, tempfile, os
from pathlib import Path

p=argparse.ArgumentParser()
p.add_argument('--server',default=r'(localdb)\MSSQLLocalDB')
p.add_argument('--database',required=True)
p.add_argument('--output',default='database/qa/test-results.json')
args=p.parse_args()
if 'verify' not in args.database.lower() and 'test' not in args.database.lower():
    raise SystemExit('Refusing mutation tests on a database without Verify/Test in its name.')
results=[]

def run(sql,expected=None):
    with tempfile.NamedTemporaryFile(mode='w',suffix='.sql',encoding='utf-8',delete=False) as f:
        f.write('SET NOCOUNT ON; '+sql)
        path=f.name
    try:
        r=subprocess.run(['sqlcmd','-S',args.server,'-d',args.database,'-E','-C','-I','-b','-f','65001','-h','-1','-W','-w','4000','-i',path],capture_output=True,text=True,encoding='utf-8',errors='replace',timeout=40)
    finally:
        os.unlink(path)
    out=(r.stdout+r.stderr).strip()
    if expected:
        if r.returncode==0 or f'Msg {expected},' not in out: raise AssertionError(f'Expected SQL error {expected}; code={r.returncode}: {out}')
    elif r.returncode: raise AssertionError(out)
    return out

def test(name,sql,expected=None):
    start=time.monotonic()
    run(sql,expected)
    results.append(dict(name=name,status='passed',seconds=round(time.monotonic()-start,3)))
    print('PASS',name,flush=True)

def create(key=None,customer=1,voucher=None,items='[{"variantId":4,"quantity":2}]',mode='Prepaid',fulfillment='Pickup',branch=1):
    key=key or str(uuid.uuid4())
    actor=5 if customer==1 else 6
    return f"EXEC dbo.CreateOrder @ActorID={actor},@BranchID={branch},@Source='App',@Fulfillment='{fulfillment}',@PaymentMode='{mode}',@Items=N'{items}',@RequestKey='{key}',@CustomerID={customer}"+(f',@CustomerPromotionID={voucher}' if voucher else '')+(f',@AddressID={customer}' if fulfillment=='Delivery' else '')+';'

def race(name,commands,expected_successes=1):
    def execute(cmd):
        try: return True,run(cmd)
        except AssertionError as e: return False,str(e)
    with concurrent.futures.ThreadPoolExecutor(max_workers=len(commands)) as pool: outcomes=list(pool.map(execute,commands))
    assert sum(ok for ok,_ in outcomes)==expected_successes,outcomes
    # A SQL deadlock or infrastructure failure is not accepted as a business validation failure.
    for ok,out in outcomes:
        if not ok: assert 'Msg 51100,' in out or 'Msg 51109,' in out,out
    results.append(dict(name=name,status='passed',outcomes=[{'succeeded':ok,'output':out} for ok,out in outcomes]))
    print('PASS',name,flush=True)

try:
    test('43 tables and trusted foreign keys',"IF (SELECT COUNT(*) FROM sys.tables)<>43 THROW 52000,'Wrong table count',1; IF EXISTS(SELECT 1 FROM sys.foreign_keys WHERE is_not_trusted=1 OR is_disabled=1) THROW 52000,'Untrusted FK',1;")
    test('Pickup price, voucher and awarded points',"IF NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderID=1 AND Subtotal=70000 AND DiscountAmount=10000 AND TotalAmount=60000 AND Status='Completed') THROW 52000,'Wrong total',1; IF NOT EXISTS(SELECT 1 FROM dbo.LoyaltyTransactions WHERE OrderID=1 AND Kind='Earn' AND Points=6) THROW 52000,'Wrong points',1;")
    test('Idempotent completion and successful payment callback',"EXEC dbo.AdvanceOrder 4,1,'Completed'; EXEC dbo.SimulatePayment 5,2,'Succeeded'; IF (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE OrderID=1 AND Kind='Earn')<>1 THROW 52000,'Duplicate points',1;")
    test('Guest POS has no customer or points',"IF NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE OrderID=3 AND CustomerID IS NULL AND Status='Completed') THROW 52000,'Guest missing',1; IF EXISTS(SELECT 1 FROM dbo.LoyaltyTransactions WHERE OrderID=3) THROW 52000,'Guest points',1;")
    test('Unpaid COD cannot complete',"EXEC dbo.AdvanceOrder 4,2,'Completed';",51100)
    test('Customer cannot acknowledge COD collection',"DECLARE @k uniqueidentifier=NEWID(); EXEC dbo.StartPayment 6,2,@k;",51103)
    test('Paid delivery completes with merchandise-only points',"DECLARE @r TABLE(ID int); DECLARE @k uniqueidentifier=NEWID(),@p int; INSERT @r EXEC dbo.StartPayment 4,2,@k; SELECT @p=ID FROM @r; EXEC dbo.SimulatePayment 4,@p,'Succeeded'; EXEC dbo.AdvanceOrder 4,2,'Completed'; IF NOT EXISTS(SELECT 1 FROM dbo.LoyaltyTransactions WHERE OrderID=2 AND Points=3) THROW 52000,'Delivery fee earned points',1;")
    for name,actor in [('other branch staff',8),('other area manager',9),('other customer',6)]: test('Deny '+name+' reading order',f'EXEC dbo.GetOrder {actor},1;',51103)
    test('Allow area manager own area',"EXEC dbo.GetOrder 2,1;")
    test('Retired branch-price procedure rejects overrides',"EXEC dbo.SetBranchPrice 3,2,1,99000,1;",51100)
    test('Reject missing required sugar/ice',create(items='[{"variantId":1,"quantity":1}]'),51100)
    test('Reject topping on cake',create(items='[{"variantId":4,"quantity":1,"optionIds":[5]}]'),51100)
    test('Reject unavailable branch option',create(branch=3,items='[{"variantId":1,"quantity":1,"optionIds":[1,3,6]}]'),51100)
    test('Reject zero quantity',create(items='[{"variantId":4,"quantity":0}]'),51100)
    test('Reject another customer address',"DECLARE @k uniqueidentifier=NEWID(); EXEC dbo.CreateOrder 5,1,'App','Delivery','COD',N'[{\"variantId\":4,\"quantity\":1}]',@k,@CustomerID=1,@AddressID=2;",51103)
    test('Historical snapshots survive menu/address changes',"UPDATE dbo.ProductVariants SET BasePrice=99000 WHERE VariantID=1; UPDATE dbo.Products SET Name=N'Tên món mới' WHERE ProductID=1; UPDATE dbo.CustomerAddresses SET AddressText=N'Địa chỉ mới' WHERE AddressID=2; IF NOT EXISTS(SELECT 1 FROM dbo.OrderDetails WHERE OrderID=1 AND UnitPrice=30000 AND ProductName=N'Cà phê sữa') THROW 52000,'Order price changed',1; IF NOT EXISTS(SELECT 1 FROM dbo.Deliveries WHERE OrderID=2 AND AddressText=N'20 đường B, TP.HCM') THROW 52000,'Delivery address changed',1;")
    key=str(uuid.uuid4()); command=create(key=key)
    test('First idempotent order request',command)
    test('Replay same order request',command+f"IF (SELECT COUNT(*) FROM dbo.Orders WHERE RequestKey='{key}')<>1 THROW 52000,'Duplicate order',1;")
    test('Reject different payload under same key',create(key=key,items='[{"variantId":4,"quantity":3}]'),51109)
    test('Gift voucher cannot be self-issued',f"EXEC dbo.IssueVoucher 5,1,2,'{uuid.uuid4()}';",51103)
    test('Branch manager cannot issue chain gift',f"EXEC dbo.IssueVoucher 3,1,2,'{uuid.uuid4()}';",51103)
    voucher=run(f"EXEC dbo.IssueVoucher 6,2,1,'{uuid.uuid4()}';").strip()
    assert voucher.isdigit(),voucher
    race('Concurrent orders cannot reserve same voucher',[create(customer=2,voucher=int(voucher)),create(customer=2,voucher=int(voucher))])
    test('Voucher has exactly one reservation',f"IF (SELECT COUNT(*) FROM dbo.Orders WHERE CustomerPromotionID={voucher})<>1 THROW 52000,'Duplicate voucher consumption',1;")
    test('Cancel releases reservation without changing expiry',f"DECLARE @id int=(SELECT ReservedOrderID FROM dbo.CustomerPromotions WHERE CustomerPromotionID={voucher}),@exp datetime2(3)=(SELECT ExpiresAt FROM dbo.CustomerPromotions WHERE CustomerPromotionID={voucher}); EXEC dbo.CancelOrder 6,@id,N'Test hủy'; IF NOT EXISTS(SELECT 1 FROM dbo.CustomerPromotions WHERE CustomerPromotionID={voucher} AND Status='Available' AND ExpiresAt=@exp) THROW 52000,'Voucher not restored',1;")
    race('Concurrent redemptions cannot overspend points',[f"EXEC dbo.IssueVoucher 5,1,1,'{uuid.uuid4()}';",f"EXEC dbo.IssueVoucher 5,1,1,'{uuid.uuid4()}';"])
    test('No overspending after concurrent redemption',"IF (SELECT LoyaltyPoints FROM dbo.Customers WHERE CustomerID=1)<>6 THROW 52000,'Wrong concurrent balance',1;")
    test('Prepare low-point voucher for reversal case',"INSERT dbo.Promotions(Name,Scope,CreatedBy,DiscountType,DiscountValue,MinSubtotal,StartsAt,EndsAt,PointsCost,TotalLimit,PerCustomerLimit) VALUES(N'Test 5 điểm','Chain',1,'Amount',1000,10000,DATEADD(day,-1,SYSUTCDATETIME()),DATEADD(day,10,SYSUTCDATETIME()),5,100,10); DECLARE @id int=SCOPE_IDENTITY(),@k uniqueidentifier=NEWID(); EXEC dbo.IssueVoucher 5,1,@id,@k;")
    test('Refund retries once and allows negative clawback balance',"DECLARE @r TABLE(ID int); DECLARE @k uniqueidentifier=NEWID(),@id int; INSERT @r EXEC dbo.RequestRefund 3,1,N'Hoàn toàn bộ đơn test',@k; SELECT @id=ID FROM @r; EXEC dbo.SimulateRefund 3,@id,'Failed',N'Lỗi test'; EXEC dbo.SimulateRefund 3,@id,'Succeeded'; EXEC dbo.SimulateRefund 3,@id,'Succeeded'; IF (SELECT LoyaltyPoints FROM dbo.Customers WHERE CustomerID=1)<>-5 THROW 52000,'Wrong reversed balance',1; IF (SELECT COUNT(*) FROM dbo.LoyaltyTransactions WHERE OrderID=1 AND Kind='Reverse')<>1 THROW 52000,'Duplicate reversal',1;")
    test('Negative balance blocks paid redemption',f"EXEC dbo.IssueVoucher 5,1,4,'{uuid.uuid4()}';",51100)
    test('Customer cannot cancel after confirmation',"DECLARE @id int=(SELECT MIN(OrderID) FROM dbo.Orders WHERE CustomerID=1 AND Status='AwaitingPayment'); DECLARE @r TABLE(ID int); DECLARE @k uniqueidentifier=NEWID(),@p int; INSERT @r EXEC dbo.StartPayment 5,@id,@k; SELECT @p=ID FROM @r; EXEC dbo.SimulatePayment 5,@p,'Succeeded'; EXEC dbo.AdvanceOrder 4,@id,'Confirmed'; EXEC dbo.CancelOrder 5,@id,N'Không được phép';",51103)
    timeout_key=str(uuid.uuid4())
    run(create(key=timeout_key))
    test('Expired unpaid order is cancelled by sweep',f"UPDATE dbo.Orders SET PaymentDueAt=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE RequestKey='{timeout_key}'; EXEC dbo.ExpireUnpaidOrders 1; IF NOT EXISTS(SELECT 1 FROM dbo.Orders WHERE RequestKey='{timeout_key}' AND Status='Cancelled') THROW 52000,'Expiry job failed',1;")
    test('Reject incomplete survey',"EXEC dbo.SubmitSurvey 6,1,2,N'[{\"questionId\":1,\"optionIds\":[1]}]';",51100)
    test('Reject invalid rating',"EXEC dbo.SubmitSurvey 6,1,2,N'[{\"questionId\":1,\"optionIds\":[1]},{\"questionId\":2,\"rating\":6}]';",51100)
    test('Reject option from outside question',"EXEC dbo.SubmitSurvey 6,1,2,N'[{\"questionId\":1,\"optionIds\":[999]},{\"questionId\":2,\"rating\":5}]';",51100)
    test('Atomic multi-choice survey submission',"EXEC dbo.SubmitSurvey 6,1,2,N'[{\"questionId\":1,\"optionIds\":[1,3]},{\"questionId\":2,\"rating\":4}]'; IF (SELECT COUNT(*) FROM dbo.SurveyAnswerOptions a JOIN dbo.SurveyResponses r ON r.ResponseID=a.ResponseID WHERE r.CustomerID=2)<>2 THROW 52000,'Missing multiple choices',1;")
    test('Reject duplicate survey submission',"EXEC dbo.SubmitSurvey 6,1,2,N'[]';",51109)
    test('Published questions are frozen',"UPDATE dbo.SurveyQuestions SET Text=N'Đổi câu hỏi' WHERE QuestionID=1;",51001)
    test('Published options are frozen',"UPDATE dbo.SurveyQuestionOptions SET Text=N'Đổi đáp án' WHERE OptionID=1;",51001)
    test('Loyalty history is immutable',"DELETE FROM dbo.LoyaltyTransactions WHERE LoyaltyTransactionID=1;",51001)
    test('Foreign keys reject incorrect variant/product',"INSERT dbo.OrderDetails(OrderID,ProductID,VariantID,ProductName,VariantName,Quantity,UnitPrice,ModifierTotal,LineTotal) VALUES(1,2,1,N'Wrong','M',1,1,0,1);",547)
    test('Setup runtime role principal',"CREATE USER MorningBrewTestRuntime WITHOUT LOGIN; ALTER ROLE MorningBrewRuntime ADD MEMBER MorningBrewTestRuntime;")
    test('Runtime cannot read raw tables',"EXECUTE AS USER='MorningBrewTestRuntime'; SELECT * FROM dbo.Users;",229)
    test('Runtime can invoke scoped procedures',"EXECUTE AS USER='MorningBrewTestRuntime'; EXEC dbo.GetOrder 4,1; REVERT;")
    race('Concurrent default address updates leave one default',["EXEC dbo.SaveAddress 5,1,N'A',N'Lan','0901234567',N'Địa chỉ A',1;","EXEC dbo.SaveAddress 5,1,N'B',N'Lan','0901234567',N'Địa chỉ B',1;"],2)
    test('Single default address invariant',"IF (SELECT COUNT(*) FROM dbo.CustomerAddresses WHERE CustomerID=1 AND IsDefault=1)<>1 THROW 52000,'Multiple defaults',1;")
    test('Financial and loyalty reconciliation',"IF EXISTS(SELECT 1 FROM dbo.LoyaltyReconciliation WHERE Difference<>0) THROW 52000,'Loyalty mismatch',1; IF EXISTS(SELECT 1 FROM dbo.Orders o CROSS APPLY(SELECT SUM(LineTotal) s FROM dbo.OrderDetails d WHERE d.OrderID=o.OrderID)x WHERE o.Subtotal<>x.s) THROW 52000,'Order subtotal mismatch',1; IF EXISTS(SELECT 1 FROM dbo.OrderDetails d OUTER APPLY(SELECT SUM(ExtraPrice) s FROM dbo.OrderItemModifiers m WHERE m.OrderDetailID=d.OrderDetailID)x WHERE d.ModifierTotal<>COALESCE(x.s,0)) THROW 52000,'Modifier totals mismatch',1;")
    test('Revenue is scoped and accounts for actual refunds',"DECLARE @f datetime2='2020-01-01',@t datetime2='2100-01-01'; DECLARE @r TABLE(BranchID int,Name nvarchar(150),CompletedOrders int,GrossRevenue decimal(18,2),RefundedRevenue decimal(18,2),NetRevenue decimal(18,2)); INSERT @r EXEC dbo.RevenueReport 1,@f,@t; IF NOT EXISTS(SELECT 1 FROM @r WHERE BranchID=1 AND GrossRevenue=155000 AND RefundedRevenue=60000 AND NetRevenue=95000) THROW 52000,'Revenue mismatch',1; DELETE @r; INSERT @r EXEC dbo.RevenueReport 9,@f,@t; IF EXISTS(SELECT 1 FROM @r WHERE BranchID IN (1,2)) THROW 52000,'Scope leak',1;")
except Exception as exc:
    results.append(dict(name='FAILED',status='failed',error=str(exc)))
    raise
finally:
    dest=Path(args.output); dest.parent.mkdir(parents=True,exist_ok=True)
    dest.write_text(json.dumps(dict(database=args.database,tests=results,passed=sum(x['status']=='passed' for x in results)),ensure_ascii=False,indent=2),encoding='utf-8')
print(f'All {len(results)} checks passed.')

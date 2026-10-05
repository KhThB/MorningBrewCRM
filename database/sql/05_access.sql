SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
CREATE OR ALTER PROCEDURE dbo.GetOrder @ActorID int,@OrderID int
AS
BEGIN
 SET NOCOUNT ON;
 DECLARE @branch int,@customer int;
 SELECT @branch=BranchID,@customer=CustomerID FROM dbo.Orders WHERE OrderID=@OrderID;
 IF @branch IS NULL OR (dbo.IsCustomer(@ActorID,@customer)=0 AND dbo.CanBranch(@ActorID,@branch,'Orders.Read')=0) THROW 51103,N'Order is not accessible.',1;
 SELECT * FROM dbo.Orders WHERE OrderID=@OrderID;
 SELECT * FROM dbo.OrderDetails WHERE OrderID=@OrderID;
 SELECT m.* FROM dbo.OrderItemModifiers m JOIN dbo.OrderDetails d ON d.OrderDetailID=m.OrderDetailID WHERE d.OrderID=@OrderID;
 SELECT * FROM dbo.Deliveries WHERE OrderID=@OrderID;
 SELECT PaymentID,Method,Amount,Status,CreatedAt,SettledAt FROM dbo.PaymentAttempts WHERE OrderID=@OrderID;
 SELECT RefundID,Amount,Status,Reason,CreatedAt,CompletedAt FROM dbo.Refunds WHERE OrderID=@OrderID;
 SELECT FromStatus,ToStatus,Reason,CreatedAt FROM dbo.OrderStatusHistory WHERE OrderID=@OrderID ORDER BY HistoryID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.ListOrders @ActorID int,@BranchID int=NULL,@CustomerID int=NULL,@Skip int=0,@Take int=50
AS
BEGIN
 SET NOCOUNT ON;
 IF @Skip<0 OR @Take NOT BETWEEN 1 AND 100 THROW 51100,N'Invalid pagination.',1;
 SELECT o.OrderID,o.OrderCode,o.BranchID,o.CustomerID,o.Status,o.Source,o.Fulfillment,o.TotalAmount,o.CreatedAt FROM dbo.Orders o
 WHERE (@BranchID IS NULL OR o.BranchID=@BranchID) AND (@CustomerID IS NULL OR o.CustomerID=@CustomerID)
 AND (dbo.IsCustomer(@ActorID,o.CustomerID)=1 OR dbo.CanBranch(@ActorID,o.BranchID,'Orders.Read')=1)
 ORDER BY o.CreatedAt DESC,o.OrderID DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;
END;
GO
CREATE OR ALTER PROCEDURE dbo.LookupCustomer @ActorID int,@BranchID int,@Phone varchar(20)
AS
BEGIN
 SET NOCOUNT ON;
 IF dbo.CanBranch(@ActorID,@BranchID,'Customers.Lookup')=0 THROW 51103,N'POS lookup is outside scope.',1;
 IF NULLIF(@Phone,'') IS NULL THROW 51100,N'Exact phone is required.',1;
 SELECT c.CustomerID,u.FullName,u.Phone,c.LoyaltyPoints FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE u.Phone=@Phone AND u.IsActive=1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.GetCustomerAccount @ActorID int,@CustomerID int
AS
BEGIN
 SET NOCOUNT ON;
 IF dbo.IsCustomer(@ActorID,@CustomerID)=0 THROW 51103,N'Account belongs to another customer.',1;
 SELECT c.CustomerID,u.FullName,u.Email,u.Phone,c.DateOfBirth,c.Gender,c.LoyaltyPoints,c.Version FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID WHERE c.CustomerID=@CustomerID;
 SELECT * FROM dbo.CustomerAddresses WHERE CustomerID=@CustomerID AND IsActive=1;
 SELECT * FROM dbo.CustomerPromotions WHERE CustomerID=@CustomerID ORDER BY IssuedAt DESC;
 SELECT * FROM dbo.LoyaltyTransactions WHERE CustomerID=@CustomerID ORDER BY LoyaltyTransactionID DESC;
 SELECT n.* FROM dbo.Notifications n WHERE n.UserID=@ActorID ORDER BY n.NotificationID DESC;
 SELECT s.*,r.CompletedAt FROM dbo.SurveyRecipients r JOIN dbo.Surveys s ON s.SurveyID=r.SurveyID WHERE r.CustomerID=@CustomerID;
END;
GO
CREATE OR ALTER PROCEDURE dbo.GetMenu @BranchID int
AS
BEGIN
 SET NOCOUNT ON;
 SELECT b.BranchID,b.Name,b.Address,b.Phone,b.OpeningTime,b.ClosingTime,b.AcceptingOrders,b.PickupEnabled,b.DeliveryEnabled,b.DeliveryFee FROM dbo.Branches b WHERE b.BranchID=@BranchID AND b.IsActive=1;
 SELECT p.ProductID,p.Name,p.Description,p.ImageURL,p.CategoryID,v.VariantID,v.Name AS VariantName,COALESCE(bv.SellingPrice,v.BasePrice) AS Price,v.DisplayOrder
 FROM dbo.BranchProducts bp JOIN dbo.Branches b ON b.BranchID=bp.BranchID AND b.IsActive=1 JOIN dbo.Products p ON p.ProductID=bp.ProductID AND p.IsActive=1
 JOIN dbo.Categories c ON c.CategoryID=p.CategoryID AND c.IsActive=1 JOIN dbo.ProductVariants v ON v.ProductID=p.ProductID AND v.IsActive=1
 LEFT JOIN dbo.BranchProductVariants bv ON bv.BranchID=bp.BranchID AND bv.VariantID=v.VariantID WHERE bp.BranchID=@BranchID AND bp.IsAvailable=1 AND COALESCE(bv.IsAvailable,1)=1 ORDER BY p.ProductID,v.DisplayOrder;
 SELECT pg.ProductID,g.ModifierGroupID,g.Name AS GroupName,g.Kind,pg.MinSelections,pg.MaxSelections,o.ModifierOptionID,o.Name AS OptionName,COALESCE(bo.ExtraPrice,o.ExtraPrice) AS ExtraPrice
 FROM dbo.ProductModifierGroups pg JOIN dbo.ModifierGroups g ON g.ModifierGroupID=pg.ModifierGroupID AND g.IsActive=1
 JOIN dbo.ModifierOptions o ON o.ModifierGroupID=g.ModifierGroupID AND o.IsActive=1 JOIN dbo.BranchProducts bp ON bp.ProductID=pg.ProductID AND bp.BranchID=@BranchID AND bp.IsAvailable=1
 LEFT JOIN dbo.BranchModifierOptions bo ON bo.BranchID=@BranchID AND bo.ModifierOptionID=o.ModifierOptionID WHERE COALESCE(bo.IsAvailable,1)=1;
END;
GO
CREATE OR ALTER PROCEDURE dbo.SegmentCustomers
 @ActorID int,@BranchID int=NULL,@MinAge int=NULL,@MaxAge int=NULL,@Gender varchar(15)=NULL,@ProductID int=NULL,@MinPoints int=NULL,@MinSpend decimal(18,2)=NULL,@InactiveDays int=NULL
AS
BEGIN
 SET NOCOUNT ON;
 -- Spending and recency are computed only from orders inside the actor's scope.
 SELECT c.CustomerID,u.FullName,c.Gender,c.DateOfBirth,c.LoyaltyPoints,agg.Spend,agg.LastPurchase
 FROM dbo.Customers c JOIN dbo.Users u ON u.UserID=c.UserID AND u.IsActive=1
 CROSS APPLY(SELECT DATEDIFF(year,c.DateOfBirth,CONVERT(date,SYSUTCDATETIME()))-CASE WHEN DATEADD(year,DATEDIFF(year,c.DateOfBirth,CONVERT(date,SYSUTCDATETIME())),c.DateOfBirth)>CONVERT(date,SYSUTCDATETIME()) THEN 1 ELSE 0 END Age)x
 OUTER APPLY(SELECT SUM(o.TotalAmount-COALESCE(r.Amount,0)) Spend,MAX(o.CompletedAt) LastPurchase FROM dbo.Orders o LEFT JOIN dbo.Refunds r ON r.OrderID=o.OrderID AND r.Status='Succeeded' WHERE o.CustomerID=c.CustomerID AND o.Status='Completed' AND (@BranchID IS NULL OR o.BranchID=@BranchID) AND dbo.CanBranch(@ActorID,o.BranchID,'Customers.Read')=1)agg
 WHERE (dbo.CanScope(@ActorID,'Chain',NULL,NULL,'Customers.Read')=1 AND @BranchID IS NULL OR agg.LastPurchase IS NOT NULL)
 AND (@MinAge IS NULL OR x.Age>=@MinAge) AND (@MaxAge IS NULL OR x.Age<=@MaxAge) AND (@Gender IS NULL OR c.Gender=@Gender)
 AND (@MinPoints IS NULL OR c.LoyaltyPoints>=@MinPoints) AND (@MinSpend IS NULL OR COALESCE(agg.Spend,0)>=@MinSpend)
 AND (@InactiveDays IS NULL OR agg.LastPurchase IS NULL OR agg.LastPurchase<DATEADD(day,-@InactiveDays,SYSUTCDATETIME()))
 AND (@ProductID IS NULL OR EXISTS(SELECT 1 FROM dbo.CustomerPreferences p WHERE p.CustomerID=c.CustomerID AND p.ProductID=@ProductID));
END;
GO
CREATE OR ALTER PROCEDURE dbo.RevenueReport @ActorID int,@FromUTC datetime2(3),@ToUTC datetime2(3),@BranchID int=NULL
AS
BEGIN
 SET NOCOUNT ON;
 IF @FromUTC>=@ToUTC THROW 51100,N'Use a nonempty half-open UTC interval.',1;
 ;WITH ledger AS (
 SELECT o.BranchID,o.CompletedAt EventAt,CONVERT(int,1) CompletedOrders,o.TotalAmount GrossAmount,CONVERT(decimal(18,2),0) RefundedAmount
 FROM dbo.Orders o WHERE o.CompletedAt IS NOT NULL
 UNION ALL
 SELECT o.BranchID,r.CompletedAt,0,0,r.Amount FROM dbo.Refunds r JOIN dbo.Orders o ON o.OrderID=r.OrderID WHERE r.Status='Succeeded' AND o.CompletedAt IS NOT NULL
 ) SELECT l.BranchID,b.Name,SUM(l.CompletedOrders) CompletedOrders,SUM(l.GrossAmount) GrossRevenue,SUM(l.RefundedAmount) RefundedRevenue,SUM(l.GrossAmount-l.RefundedAmount) NetRevenue
 FROM ledger l JOIN dbo.Branches b ON b.BranchID=l.BranchID WHERE l.EventAt>=@FromUTC AND l.EventAt<@ToUTC AND (@BranchID IS NULL OR l.BranchID=@BranchID) AND dbo.CanBranch(@ActorID,l.BranchID,'Reports.Read')=1 GROUP BY l.BranchID,b.Name;
END;
GO
CREATE OR ALTER VIEW dbo.LoyaltyReconciliation AS
 SELECT c.CustomerID,c.LoyaltyPoints AS CachedPoints,COALESCE(SUM(l.Points),0) AS LedgerPoints,c.LoyaltyPoints-COALESCE(SUM(l.Points),0) AS Difference FROM dbo.Customers c LEFT JOIN dbo.LoyaltyTransactions l ON l.CustomerID=c.CustomerID GROUP BY c.CustomerID,c.LoyaltyPoints;
GO
-- This role is for a trusted backend, NOT end users. Identity must be supplied from verified auth claims.
-- Metadata CRUD not covered by procedures is performed by a separate trusted administration service.
CREATE ROLE MorningBrewRuntime;
DENY SELECT,INSERT,UPDATE,DELETE ON SCHEMA::dbo TO MorningBrewRuntime;
GRANT EXECUTE ON SCHEMA::dbo TO MorningBrewRuntime;
GO

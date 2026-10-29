using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace OrderProcessing.Shared.Storage;

public static class DataFiles
{
    public const string Customers = "customers.json";
    public const string Products = "products.json";
    public const string Coupons = "coupons.json";
    public const string IncomingOrders = "incoming_orders.json";
    public const string Orders = "orders.json";
    public const string Payments = "payments.json";
    public const string Emails = "emails.json";
    public const string EmailRetry = "email_retry.json";
    public const string Invoices = "invoices";
    public const string Reports = "reports";


    public static string Root => Path.Combine(AppContext.BaseDirectory, "Data");

    public static string PathFor(string fileName) => Path.Combine(Root, fileName);


}

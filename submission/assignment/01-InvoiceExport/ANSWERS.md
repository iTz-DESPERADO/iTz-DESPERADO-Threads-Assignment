<div dir="rtl" align="right">

# Assignment 01 — Answers

**Student name**: Gamal Khaled Mohamed  
**Group / ID**: G4

## المشاكل في التصميم الحالي (Problems with the current design)

1. **حجز الـ Threads (Blocking Threads):**  
   الكود الاصلي بيستخدم `Thread.Sleep()` علشان يحاكي وقت انتظار رد قاعدة البيانات المشكلة ان الـ Thread بتفضل محجوزه طول فترة الانتظار رغم انها مش بتنفذ شغل وده بيهدر موارد ممكن نستفيد منها في عمليات تانية

2. **عمليات كتابة ملفات متزامنة (Synchronous File Operations):**  
   مرحلة تجهيز ملفات العملاء (Seeding) بتستخدم كتابة متزامنة يعني الـ Thread اللي بينفذ العملية بيتستنى لحد ما الكتابة تخلص قبل ما تكمل

3. **تصدير الفواتير بالتتابع (Sequential Invoice Export):**  
   الكود الاصلي بيعالج كل طلب لوحده ومش بيبدا الطلب اللي بعده الا بعد انتهاء اللي قبله علشان كده اوقات انتظار الـ I/O بتتجمع على بعضها وبتزود الوقت الكلي للتصدير

4. **ضعف قابلية التوسع (Poor Scalability):**  
   مع زيادة عدد الطلبات اسلوب التنفيذ المتتابع وحجز الـ Threads اثناء الانتظار هيخلوا عملية التصدير ابطا وهيصعبوا الاستفاده من موارد البرنامج بكفاءة

## التعديلات اللي عملتها (What you changed)

1. **تحويل عمليات قاعدة البيانات الى Async:**
   - حوّلت `GetPendingOrders()` و`GetOrderById()` الى ميثودز غير متزامنة، بترجع `Task<List<Order>>` و`Task<Order?>`
   - استبدلت `Thread.Sleep()` بـ `await Task.Delay()` لمحاكاة التاخير من غير حجز Thread طول فترة الانتظار

2. **تحويل تجهيز ملفات العملاء الى Async:**
   - استبدلت الكتابة المتزامنة بـ `File.WriteAllTextAsync()`
   - بدات كل مهام الكتابة الاول، وجمعتها في `List<Task>` وبعدها عملت `await` لكل Task على حدة

3. **تشغيل تصدير الفواتير بشكل Concurrent:**
   - حوّلت خدمة التصدير لاستخدام `async/await`.
   - بدات Task مستقلة لكل Order وخزنت الـ Tasks في `List<Task>`
   - بعد ما بدات كل العمليات، انتظرت اكتمال كل Task واحده واحده **من غير استخدام `Task.WhenAll()`**

4. **الحفاظ على تسلسل العمل (Business Flow):**
   - جوه كل طلب حافظت على نفس الترتيب: **تحميل الطلب ← قراءة ملف العميل ← كتابة الفاتورة**
   - الخطوات جوه الطلب الواحد بتتم بالتتابع لانها معتمدة على بعضها لكن الطلبات المختلفة ممكن تتنفّذ في نفس الوقت بشكل Concurrent

## مخرجات التشغيل قبل وبعد (Console Output Before and After)

### قبل التعديل (Synchronous)

</div>
<div dir="ltr" align="left">

```text
Starting invoice export...
Customer files: C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices

Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1001.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1002.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1003.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1004.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1005.txt
Export finished in 5.9 s
```

</div>
<div dir="rtl" align="right">

### بعد التعديل (Asynchronous / Concurrent)

</div>
<div dir="ltr" align="left">

```text
Starting invoice export...
Customer files: C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\Data
Invoice output: C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices

Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1005.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1003.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1004.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1002.txt
Wrote C:\Users\gamal\Downloads\Compressed\AdvancedCsharp-Assignment-01-InvoiceExport-main Done\AdvancedCsharp-Assignment-01-InvoiceExport-main\InvoiceExport\bin\Debug\net9.0\invoices\invoice-1001.txt
Export finished in 1.8 s
```

</div>
<div dir="rtl" align="right">

## مقارنة الأداء (Performance Comparison)

| المقياس | قبل التعديل | بعد التعديل |
|---|---|---|
| طريقة التنفيذ | Sequential | Concurrent |
| زمن التصدير | 5.9 ثانية | 1.8 ثانية |
| عدد الفواتير الناتجة | 5 | 5 |

- **الوقت اللي وفرته:** 4.1 ثانية
- **نسبة تقليل زمن التنفيذ:** حوالي **69.49%**
- **معدل التسريع:** حوالي **3.28×**

</div>

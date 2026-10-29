# Assignment 05 — Views and Emails

**Student name:**

**Group / ID:**

## What is wrong

المشكلة هي **Race Condition** بسبب استخدام `Dictionary` غير امنة للتعديل المتزامن

اثناء التشغيل ظهرت مشكلتان  : 

- **InvalidOperationException :**  بسبب تعديل عدة Threads للـ Dictionary في نفس الوقت، مما ادى الى Corrupted State
- **ArgumentException:** بسبب محاولة اضافة نفس الـ Key من اكثر من Thread

## How I fixed it

استبدلت `Dictionary` بـ `ConcurrentDictionary` واستخدمت `AddOrUpdate` لضمان تحديث عدد المشاهدات بشكل Atomic ومنع ضياع التحديثات

واستخدمت `ConcurrentQueue` للـ Email Jobs و`lock` لحماية `SentEmails` اثناء الاضافة

## Collection safety vs business-rule safety

لان الـ Thread-Safe Collection بتحمي عملياتها الفردية فقط مش الـ Business Logic بالكامل

مثلا لو عندي شرط يفحص هل الايميل اتبعت قبل ما اضيفه ممكن اكثر من Thread تتجاوز الشرط في نفس الوقت

في الحالة دي لازم احمي عمليتي الفحص والاضافة مع بعض

## Console Output

### Before

```text
System.ArgumentException:
An item with the same key has already been added.
Key: koshary-bowl

System.InvalidOperationException:
A concurrent update was performed on this
collection and corrupted its state.
```

### After

```text
Views expected=50000, actual=50000
Email jobs placed=20, unique sent=20, total sends=20
```



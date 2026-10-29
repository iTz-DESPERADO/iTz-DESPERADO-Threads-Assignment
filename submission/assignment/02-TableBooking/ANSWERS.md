# Assignment 02 — Answers

**Student name:** Gamal Khaled Mohamed

**Group / ID:** G4

## What is wrong

المشكلة هي **Race Condition** لان اكثر من Thread تقدر تقرا `TablesLeft` وتعدلها في نفس الوقت بدون حماية

وده سبب نتائج غير صحيحة زي حجز اكثر من 5 طاولات ووصول `TablesLeft` لقيم سالبة

## How you fixed it

استخدمت `lock` لحماية الـ Critical Section بحيث Thread واحدة بس تقدر تفحص عدد الطاولات وتحجز في نفس الوقت

وبكده ضمنت إن عمليتي `TablesLeft--` و`Booked++` تتم بشكل امن مع الحفاظ على شرط توفر الطاولات

## Console Output Before and After

### Before

```text
Booked = 34 , TablesLeft = -23   ** First Run **

Booked = 6  , TablesLeft =  4    ** Second Run **
 
Booked = 11 , TablesLeft = -6    ** Thrid Run **

Booked = 1  , TablesLeft =  4    ** Fourth Run **
```

### After

```text
Customer-01 booked a table Left=4
Customer-03 booked a table Left=3
Customer-02 booked a table Left=2
Customer-04 booked a table Left=1
Customer-05 booked a table Left=0

Booked=5, TablesLeft=0

Now we are granted we have same result in every run 
```
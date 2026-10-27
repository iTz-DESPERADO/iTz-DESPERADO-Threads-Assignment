<div dir="rtl" align="right">

# Assignment 04 — Menu Views

**Student name:** Gamal Khaled Mohamed

**Group / ID:** G4

## What is wrong

المشكلة هي **Race Condition**  لان العملية `Views++` مش Atomic  فهي عبارة عن قراءة وتعديل وكتابة

لما عدة Threads تنفذها مع بعض ممكن تحديثات تضيع وبالتالي العدد النهائي يكون اقل من المتوقع

## How I fixed it

استخدمت `InterlockedIncrement(ref Views)` لضمان تنفيذ الزيادة بشكل **Atomic** بدون الحاجة إلى `lock`

اخترت `Interlocked` لانها اخف وانسب للعمليات البسيطة على Counter واحد

اما لو عندي عدة عمليات مرتبطة لازم تتنفذ كوحدة واحدة استخدم `lock` لحماية الـ Critical Section بالكامل

## Console Output

### Before

```text
Expected: 400000
Actual:   124997
```

### After

```text
Expected: 400000
Actual:   400000
```
</div>
<div dir="rtl" align="right">

<h1>Assignment 03 — Locking Research</h1>

<p><strong>Student name:  </strong> Gamal Khaled Mohamed  </p>  
<p><strong>Group / ID:  </strong>  G4 </p> 

<h2>1</h2>
<p>يستخدم للتحكم في وصول الـ Threads الى <strong>Critical Section</strong> بحيث لا ينفذها اكثر من Thread في نفس الوقت باستخدام نفس كائن القفل لمنع الـ Race Condition</p>

<h2>2</h2>
<p>مع <code>lock(object)</code> يستخدم المترجم <code>Monitor.Enter</code> و<code>Monitor.Exit</code> داخل <code>try/finally</code> لضمان تحرير القفل حتى لو حدث Exception</p>
<p><strong>ملاحظة:</strong>  مع <code>System.Threading.Lock</code> في C# الحديثة يستخدم <code>EnterScope()</code> بدلا من <code>Monitor</code></p>

<h2>3</h2>
<ol>

<li><code>lock</code> يحرر القفل تلقائيا بينما استخدام <code>Monitor.Enter/Exit</code> يدويا عايز <code>try/finally</code></li>

<li><code>Monitor</code> يوفر تحكما اضافيا مثل <code>TryEnter</code> و<code>Wait</code> و<code>Pulse</code> اما <code>lock</code> فاسهل لحماية العمليات العادية</li>
</ol>

<h2>4</h2>
<ul>
<li><code>TryEnter</code>: عندما لا نريد الانتظار الى اجل غير محدد للحصول على القفل</li>
<li><code>Wait</code>: عندما ننتظر تحقق شرط معين يحرر الـ Monitor مؤقتا ثم يستعيده قبل اكمال التنفيذ</li>
<li><code>Pulse</code>: لابلاغ Thread منتظرة بان الحالة تغيرت ولا يحرر القفل فورا.</li>
</ul>

<h2>5</h2>

</div>

```csharp
using System.Threading;

public sealed class Counter
{
    private readonly object _sync = new();
    private int _count;

    // Using lock
    public void IncrementWithLock()
    {
        lock (_sync)
        {
            _count++;
        }
    }

    // Using Monitor
    public void IncrementWithMonitor()
    {
        bool lockTaken = false;
        try
        {
            Monitor.Enter(_sync, ref lockTaken);
            _count++;
        }
        finally
        {
            if (lockTaken)
                Monitor.Exit(_sync);
        }
    }
}
```

<div dir="rtl" align="right">

<h2>References</h2>
<ol>
<li><a href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/statements/lock">Microsoft Learn — lock statement</a></li>
<li><a href="https://learn.microsoft.com/en-us/dotnet/api/system.threading.monitor">Microsoft Learn — Monitor class</a></li>
</ol>

</div>

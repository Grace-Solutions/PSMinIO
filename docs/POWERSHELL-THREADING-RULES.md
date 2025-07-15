# PowerShell Cmdlet Threading Rules and Design Patterns

## 🚨 CRITICAL THREADING RULE

**PowerShell cmdlets can ONLY call Write-Progress, Write-Verbose, Write-Object, Write-Error from the main cmdlet thread.**

**❌ NEVER call these methods from:**
- Background threads (`Task.Run`, `Task.Factory.StartNew`)
- Async callbacks (`async/await` continuations)
- Timer callbacks
- Event handlers from other threads
- Any thread other than the main cmdlet thread

**✅ ONLY call these methods from:**
- `BeginProcessing()` override
- `ProcessRecord()` override  
- `EndProcessing()` override
- Main cmdlet thread execution

## 🔧 DESIGN PATTERN: ThreadSafeProgressCollector

### Problem
When performing background operations (file uploads, downloads, processing), you need to report progress from background threads, but PowerShell doesn't allow direct calls to Write-Progress from those threads.

### Solution
Use the **ThreadSafeProgressCollector** pattern:

```csharp
// Background thread - QUEUE updates (thread-safe)
_progressCollector.QueueProgressUpdate(activityId, "Processing", "Status", percentage);
_progressCollector.QueueVerboseMessage("Processing file: {0}", fileName);

// Main thread - PROCESS queued updates
_progressCollector.ProcessQueuedUpdates(); // Only call from main thread!
```

### Implementation Pattern

```csharp
public class MyLongRunningCmdlet : PSCmdlet
{
    private ThreadSafeProgressCollector _progressCollector;

    protected override void BeginProcessing()
    {
        _progressCollector = new ThreadSafeProgressCollector(this);
    }

    protected override void ProcessRecord()
    {
        // Start background work
        var tasks = new List<Task>();
        
        for (int i = 0; i < workItems.Count; i++)
        {
            var task = Task.Run(() =>
            {
                // ✅ CORRECT: Queue from background thread
                _progressCollector.QueueProgressUpdate(1, "Processing", $"Item {i}", progress);
                _progressCollector.QueueVerboseMessage("Processing item {0}", i);
                
                // ❌ WRONG: Never call directly from background thread
                // WriteProgress(...); // This will cause threading errors!
                // WriteVerbose(...);  // This will cause threading errors!
            });
            tasks.Add(task);
        }

        // ✅ CORRECT: Process updates from main thread with periodic updates
        while (!Task.WaitAll(tasks.ToArray(), 1000)) // 1 second intervals
        {
            _progressCollector.ProcessQueuedUpdates(); // Safe on main thread
        }
        
        // Final processing
        _progressCollector.ProcessQueuedUpdates();
    }
}
```

## 🚨 COMMON MISTAKES TO AVOID

### ❌ Mistake 1: Direct calls from background threads
```csharp
Task.Run(() =>
{
    WriteProgress(...); // THREADING ERROR!
    WriteVerbose(...);  // THREADING ERROR!
});
```

### ❌ Mistake 2: ProcessQueuedUpdates from background threads
```csharp
Task.Run(() =>
{
    _progressCollector.QueueProgressUpdate(...);
    _progressCollector.ProcessQueuedUpdates(); // THREADING ERROR!
});
```

### ❌ Mistake 3: Async/await callbacks
```csharp
await SomeAsyncOperation().ContinueWith(task =>
{
    WriteProgress(...); // THREADING ERROR!
});
```

## ✅ CORRECT PATTERNS

### Pattern 1: Periodic Processing
```csharp
var tasks = StartBackgroundTasks();
while (!Task.WaitAll(tasks, 1000))
{
    _progressCollector.ProcessQueuedUpdates(); // Every 1 second
}
_progressCollector.ProcessQueuedUpdates(); // Final update
```

### Pattern 2: Completion-based Processing
```csharp
var tasks = StartBackgroundTasks();
Task.WaitAll(tasks);
_progressCollector.ProcessQueuedUpdates(); // After completion
```

### Pattern 3: Manual Processing Points
```csharp
foreach (var item in items)
{
    ProcessItemInBackground(item); // Queues updates
    _progressCollector.ProcessQueuedUpdates(); // Process after each item
}
```

## 🎯 ERROR SYMPTOMS

If you violate these rules, you'll see errors like:
- "The WriteObject and WriteError methods cannot be called from outside the overrides..."
- "WriteProgress can only be called from within the same thread"
- Cmdlet failures with threading exceptions
- Progress bars not updating or appearing

## 📋 CHECKLIST FOR NEW CMDLETS

Before implementing background operations:

- [ ] Are you using ThreadSafeProgressCollector?
- [ ] Are all Write* calls from main thread only?
- [ ] Are you calling ProcessQueuedUpdates() only from main thread?
- [ ] Do you have periodic progress processing for long operations?
- [ ] Have you tested with verbose output enabled?
- [ ] Have you tested with progress bars enabled?

## 🔧 DEBUGGING TIPS

1. **Enable verbose logging** to see threading violations
2. **Test with progress bars** - they're most sensitive to threading issues
3. **Use Task.WaitAll with timeouts** for periodic processing
4. **Never call ProcessQueuedUpdates from callbacks**
5. **Queue everything from background threads, process from main thread**

## 📚 RELATED PATTERNS

- **File Upload/Download**: Use periodic processing during Task.WaitAll
- **Multipart Operations**: Queue from parallel tasks, process periodically
- **Long-running Operations**: Process updates every 1-2 seconds
- **Collection Processing**: Process after each item or batch

## 🎯 REMEMBER

**The golden rule: Background threads QUEUE, main thread PROCESSES.**

This pattern ensures PowerShell cmdlet compliance while providing real-time progress updates to users.

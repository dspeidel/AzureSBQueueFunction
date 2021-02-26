# AzureSBQueueFunction
Repo to demonstrate weird queue problems in an Azure function with .NET Core 3

Posted an issue here: https://github.com/Azure/azure-functions-servicebus-extension/issues/137

I have a function with an HTTP trigger that queues an item.  The same function has a Queue trigger that is meant to kick off a long-running process.  May take 5 seconds, may take 5 minutes.  As such, when the queue trigger runs, I get the message and complete it, then proceed to do the processing, creating a new queue message if I feel the need to.  My expectation here is that I've essentially said `Thank you Mr. Service Bus.  I have the message so please go away and let me work. I don't need you to manage this message anymore.`

**Lock error even though I've Completed the message**
I have an Azure Service Bus queue set up using Service Bus Explorer.  Lock timeout appears to be 1 minute. It looks like anything under 90 seconds happily processes as expected. Over 90 seconds and the console window always shows an error like `Message processing error (Action=RenewLock....`, but the function doesn't seem to fail, just logs this error.
The linked repo has ways to simulate work and queue up multiple items and complete the message before or after processing.  Most basic way to repro the lock error is to use your favorite HTTP client and:
```
HTTP POST {localhost}/queueRequest?howmany=1
{
    "DelaySeconds":100,
    "ThrowError":false,
    "CompleteBeforeProcessing": true
}
```
**Duplication via reprocessing**
If I have a few items in the queue (say, 10), it appears to get even worse, abandoning and reprocessing queue messages, even though I've already completed the message.  I see this in the logs: `DeliveryCount: 2`. In my actual function that does real work it eventually sends an email and this retry is particularly annoying as it will send multiple emails. This can be reproduced with:
```
HTTP POST {localhost}/queueRequest?howmany=10
{
    "DelaySeconds":100,
    "ThrowError":false,
    "CompleteBeforeProcessing": true
}
```
Any help or insight is greatly appreciated

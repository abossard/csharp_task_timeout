# How to cancel tasks

*TL:DR* Please make sure you use and pass down a CancellationToken.

It's not possible to cancel a running Task. So a long running function needs to accept a cancellation token and be implemented in a way it can act then the token is triggered.
# qlik-set-task-retries
Basic usage information:
```
Usage:   SetTaskRetries.exe --ntlm   <url> [--retries <retry cnt>] [--apply]
         SetTaskRetries.exe --direct <url> <port> <userDir> <userId> [--certs <path>] [--retries <retry cnt>] [--apply]
Example: SetTaskRetries.exe --ntlm   https://my.server.url --retries 3 --apply
         SetTaskRetries.exe --direct https://my.server.url 4242 MyUserDir MyUserId --certs C:\Tmp\MyCerts --retries 3
```

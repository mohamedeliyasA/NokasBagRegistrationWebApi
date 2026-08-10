

# 1 . Warnings and failures, last 30 minutes


union traces, exceptions, requests
| where timestamp > ago(30m)
| where (itemType == "trace" and severityLevel >= 2)
   or itemType == "exception"
   or (itemType == "request" and success == "False")
| project timestamp, itemType, severityLevel, message, resultCode, customDimensions
| order by timestamp desc
severityLevel: 2 = Warning, 3 = Error, 4 = Critical. The requests branch catches failed HTTP calls (4xx/5xx) even on the rare path where nothing got logged explicitly.

# 2. By a specific correlation ID


union traces, exceptions, requests
| where timestamp > ago(1d)
| where customDimensions.CorrelationId == "PASTE-CORRELATION-ID-HERE"
| project timestamp, itemType, severityLevel, message, resultCode
| order by timestamp asc
Ordered ascending here (not descending) since for one request you want to read the story top to bottom.

# 3. By a specific client ID


union traces, exceptions, requests
| where timestamp > ago(1d)
| where customDimensions.ClientId == "PASTE-CLIENT-ID-HERE"
| project timestamp, itemType, severityLevel, message, resultCode, customDimensions.CorrelationId
| order by timestamp desc
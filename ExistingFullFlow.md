# Understand the Existing Bag Registration Flow Before Implementation

Before we start any implementation, I want us to go through the existing solution **very carefully and systematically**.

The most important thing at this stage is **understanding the full picture correctly**. Do not start making implementation decisions based on assumptions.

I want you to:

1. Understand the existing business and technical flow.
2. Understand exactly what the new API is supposed to do.
3. Understand which existing functionality we are intentionally reusing.
4. Identify anything that is unclear or missing.
5. Ask me all necessary questions before implementation.
6. Do not invent, assume, redesign, or "improve" existing behavior unless I explicitly ask you to.
7. Do not start coding until you are confident that we have the same understanding of the complete flow.

I will first explain the background and the existing flow. After that, I will explain what happens after BizTalk picks up the message. Only when you have the complete picture should we discuss implementation.

---

# 1. Existing Background

Customers currently use the **Cash Portal UI** to submit bag registrations.

The existing flow is approximately:

```text
Customer
   |
   v
Cash Portal UI
   |
   v
Cash Portal Backend / NokasMwCacheDbV3
   |
   v
BizTalk
   |
   +---- Local currency (NOK) ----> Nattsafe
   |
   +---- Foreign currency (Valua) -> AX
```

The Cash Portal has its own backend/database:

**NokasMwCacheDbV3**

The Cash Portal UI backend uses this database and the existing integration flow to send registration information to downstream systems.

---

# 2. Scope of the New API project who will use the API to register the local currency bag.

For the new API project, **ONLY local currency (NOK) is in scope**.

Foreign currency / Valua / AX is **NOT part of the API scope**.

Therefore, when reasoning about the new API flow:

* Focus only on NOK.
* Completely ignore AX valuta flow.
* Do not design an API flow for foreign currency.
* Do not introduce AX/Valua handling into the API flow.
* The existing foreign-currency flow is only background information.

The API should ultimately feed into the existing NOK downstream processing.

---

# 3. Important Architectural Principle

We do **not** want to reinvent the existing downstream business logic.

The existing BizTalk solution already contains the functionality required to process a bag registration after BizTalk receives it.

The intention is to **reuse the existing BizTalk/downstream functionality as much as possible**.

The existing Cash Portal UI already reaches a BizTalk service when a customer submits a registration.

The new API should deliver its message through Service Bus, and BizTalk will pick up that message.

After BizTalk picks it up, the intention is for BizTalk to call the **same existing service/endpoint** that the Cash Portal UI currently uses.

Conceptually:

```text
Existing flow:

Customer
   |
   v
Cash Portal UI
   |
   v
Existing BizTalk service
   |
   v
Existing downstream logic
   |
   v
Nattsafe
```

New API flow:

```text
External Customer
   |
   v
New API
   |
   v
Service Bus
   |
   v
Existing BizTalk
   |
   v
Same existing BizTalk service/endpoint
   |
   v
Existing downstream logic
   |
   v
Nattsafe
```

The key objective is:

> **Reuse the existing BizTalk/downstream logic rather than recreating the same business logic inside the new API.**

We also **do not want to modify the existing BizTalk implementation unnecessarily**.

Ideally, both sources:

* Cash Portal UI
* New API

will eventually reach the same existing BizTalk service/processing path.

This should allow the existing downstream processing to continue without unnecessary changes.

---

# 4. Customer Mapping

From API, when the message delivered to biztalk, before hit the service biztalk do this customer mapping and then hit the serivce.

There is an important customer-mapping step before the registration reaches the downstream processing.

For example, we have a customer mapping record like this:

```text
RecId:                  1
ClientName:             JemNFix
ClientID:               1a732120-1149-4d97-98b5-d454d144b383
NokasCustomerNumber:    314211
NokasCustomerName:      JEM & FIX NORGE Avd Fredrikstad
CustomerRefNumber:      5790002334764
CustomerRefName:        Jem & Fix 804 Fredrikstad
AXProjectId:             172424-10-02-01
Country:                Norway
CreatedAt:              2025-08-13 20:36:01.653
CreatedBy:              Kamal
UpdatedAt:              NULL
UpdatedBy:              NULL
```

The important relationship here is:

```text
API customer identifier:
5790002334764

        |
        | Customer mapping
        v

Nokas Customer Number:
314211
```

So, if the API receives:

```text
5790002334764
```

the customer mapping must resolve this to:

```text
314211
```

and **314211 is the value that is sent to the downstream systems**.

Please treat this distinction as important:

* `CustomerRefNumber` = external/customer-facing reference
* `NokasCustomerNumber` = internal Nokas customer number used downstream
* `ClientID` = identifies the API client/customer in the API context


---

# 5. Existing BizTalk Processing

Once BizTalk receives the registration, it performs the existing processing.

The first important database operation is the stored procedure:

```text
F:\Avarn\Git\NokasBagRegistrationWebApi\SQL\SaveRegistrationV2.sql
```

This saves the registration into the **Cash Portal UI backend database**.

After that, the actual downstream registration processing is performed through:

```text
F:\Avarn\Git\NokasBagRegistrationWebApi\SQL\mwPutRegistrationV1.sql
```

This is the important existing downstream procedure.

The intention is that the new API ultimately reaches this existing processing path rather than implementing a completely new version of the downstream logic.

---

# 6. Result in Nattsafe

When the API-originated registration goes through the existing BizTalk/downstream processing and `mwPutRegistrationV1.sql` is executed, the registration is created in the Nattsafe archive.

For example:

```text
intId                  = 5901001
intInnkastnr           = 20250822160
intBagID               = NULL
vchEasysafeaccount     = 60210631464
vchCustomer_id         = 314211
vchName                =
intLocation_id         = 7
vchLocation            = Oslo
vchButikkNr            = 804
vchKKText              =
dtmRegDate             = 2026-08-04 22:55:00
vchRef                 = NULL
dblAmount              = 10000
dblSeddel1000          = 1000
dblSeddel500           = 0
dblSeddel200           = 0
dblSeddel100           = 0
dblSeddel50            = 0
dblAmountSeddel        = 0
dblAmountMynt          = 0
dblAmountCash          = 1000
dblAmountReserve       = 0
dblAmountAnnet         = NULL
dblForCurrencies       = 0
vchBoxnumber           = NULL
intPlombnumber         = NULL
intStatus              = 1
vchStatusDescription   = anbefalet
dtmSubmitdate          = 2026-08-04 22:55:00
vchConfirmedStatus     = NULL
vchStatusComments      = NULL
vchMyReference         = API_1a732120-1149-4d97-98b5-d454d144b383
dtmOmsetningsdato      = 2025-08-14 00:00:00
dtmTransportspecification = NULL
bitConfirmed           = NULL
dblControlledAmount    = NULL
dtmControlledAt        = NULL
dtmTransportFetch      = NULL
SOnumber                =
bitRequireApproval     = 0
```

One particularly important field is:

```text
vchCustomer_id = 314211
```

This demonstrates that the external customer reference:

```text
5790002334764
```

was mapped to the Nokas customer number:

```text
314211
```

before the downstream processing.

Another important field is:

```text
vchMyReference = API_1a732120-1149-4d97-98b5-d454d144b383
```

This represents the API/client-related reference that is ultimately stored in Nattsafe.

---

I put the relevant table strucutre and sql stored proc in F:\Avarn\Git\NokasBagRegistrationWebApi\SQL

# 7. BizTalk Schema

The schema that defines how the API message should be delivered to BizTalk is:

F:\Avarn\Git\NokasBagRegistrationWebApi\BizTalk\BagRegistration.xsd

This XSD is extracted from the existing BizTalk solution, so please inspect it carefully and use it as the source of truth for the BizTalk message structure.

Important points:

Root node: APIBagRegistration
Namespace: http://APICustomers.Schemas.APIBagRegistrationSchema
The BizTalk receive pipeline adds the root node and namespace.
The API's responsibility is only to deliver the JSON message body: bagRegistration.

So conceptually:

API
  → JSON: bagRegistration
  → Service Bus
  → BizTalk Receive Pipeline
  → APIBagRegistration + namespace
  → Existing BizTalk processing

If the XSD, current API implementation, or my description contradict each other, do not assume which one is correct—ask me first.

# 8. Preserve Existing API Business Requirements

The business requirements and existing behavior of the current API must be preserved.

The rule is:

Preserve the business requirements and behavior, not necessarily the existing code or design.

If you identify any existing behavior that appears problematic, or if the new implementation would cause any deviation from the current business behavior, stop and clarify with me before making the change. Do not make assumptions.
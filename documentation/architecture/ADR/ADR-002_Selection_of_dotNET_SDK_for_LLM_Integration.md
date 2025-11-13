# **ADR-02: Selection of .NET SDK for LLM Integration**

**Title:** Selection of .NET SDK for LLM Integration – `Azure.AI.OpenAI` vs `OpenAI`
**Status:** Proposed / Approved (select one)
**Date:** 2025-11-13
**Author:** Stephen Edwards (Product/Technical Architect)

---

## **1. Context**

The Interview Assistant application requires real-time LLM inference to answer coding questions (C#, .NET, architecture) and to process transcription output from Azure Cognitive Services. The system is deployed within an enterprise Azure environment and must comply with internal governance, enterprise security, and Azure networking controls.

Two different .NET SDKs are available for interacting with OpenAI-family models:

1. **`Azure.AI.OpenAI`** — for Azure OpenAI Service
2. **`OpenAI`** — for OpenAI’s public cloud API (api.openai.com)

Model deployments for this system will be hosted **exclusively in Azure OpenAI Service**, not in public OpenAI Cloud.

An ADR is required to select the correct NuGet package and standardize the SDK usage across the product architecture.

---

## **2. Decision**

**Use the `Azure.AI.OpenAI` NuGet package as the *standard and exclusive* SDK for all LLM interactions in the Interview Assistant application.**

This includes:

* All calls to Chat Completions API
* All calls to the **Responses API** (e.g., `GetResponseAsync`)
* All real-time and low-latency inference tasks
* All deployments of models such as `gpt-4o-mini`, `gpt-4.1`, `gpt-4o`, and `gpt-5-mini` on **Azure OpenAI Service**

---

## **3. Justification**

### ✔ **Required for Enterprise Azure Hosting**

The application runs entirely inside Azure (Speech Services, Functions, API apps, VNet-integrated containers).
Only `Azure.AI.OpenAI` supports:

* Azure resource endpoint integration
* Azure AD (Managed Identity) authentication
* VNet + Private Endpoint routing
* Azure cost management and quota enforcement
* Governance aligned with corporate policy

### ✔ **Correct API for Azure-Hosted Responses Models**

Azure partners with OpenAI but exposes the **Responses API** through the Azure SDK, not through the OpenAI SDK.
Models such as **`gpt-5-mini`** and **`gpt-4o-mini`** require the Azure Responses client:

```csharp
client.GetResponseAsync(deploymentName, new ResponseOptions {...});
```

The public `OpenAI` SDK does **not** support Azure deployments.

### ✔ **Consistent with Fresenius and SAFe Architecture Governance**

* Internal standards use **Azure-first services**
* Architecture documents (PAD/SAD) require Azure-consistent tooling
* Enterprise security mandates Azure-based authentication flows
* Using public OpenAI Cloud is not allowed for PHI/PII scenarios

### ✔ **Latency requirements met**

`gpt-4o-mini` and `gpt-5-mini` on Azure achieve **low-latency streaming**, meeting the requirement for real-time interview feedback.

---

## **4. Alternatives Considered**

### **Alternative 1 — Use the `OpenAI` package (public OpenAI Cloud)**

**Rejected**

* No Azure AD auth
* No private networking
* Data leaves the enterprise boundary
* Does not support Azure models or Azure-hosted deployments
* Violates enterprise security constraints
* Not compatible with the Interview Assistant architecture pipeline

### **Alternative 2 — Use both SDKs (hybrid model)**

**Rejected**

* Introduces complexity and divergence
* No benefit since all inference is Azure-based
* Confusing for developers and maintainers
* Violates “single SDK standardization” guideline in code governance

---

## **5. Consequences**

### **Positive**

* Single, standardized SDK for all LLM interactions
* Secure, compliant, Azure-governed usage
* Simplified codebase and service integration
* Compatible with enterprise authentication (MSI, Key Vault)
* Fully compatible with Azure model deployments
* Enables use of the **Responses API**, required for low-latency operations

### **Negative**

* Cannot access OpenAI Cloud–only features (if Azure support lags)
* Release cadence of Azure models may be slower than public OpenAI

---

## **6. Implementation Plan**

1. Add NuGet package:

   ```
   dotnet add package Azure.AI.OpenAI
   ```
2. Standardize the LLM client wrapper to use `OpenAIClient`
3. Ensure usage of **Responses API** for all real-time Q&A
4. Update documentation (SAD/SAD) to reflect the SDK decision
5. Enforce via code review and static analysis rule (optional)

---

## **7. Related Documents**

* PAD: Interview Assistant Architecture v1.0
* SAD: Interview Assistant LLM Pipeline v1.0
* ADR-XXX: Model Deployment Strategy for gpt-5-mini
* ADR-XXX: Real-time Audio Pipeline Architecture
* Azure Governance and Compliance Standards

---

## **8. Approval**

| Role                               | Name  | Approval |
| ---------------------------------- | ----- | -------- |
| Product Architect                  | *TBD* | □        |
| System Architect (Shared Services) | *TBD* | □        |
| Cybersecurity                      | *TBD* | □        |
| Data Privacy                       | *TBD* | □        |
| Dev Lead                           | *TBD* | □        |

---

If you'd like, I can also generate the corresponding **SAD entry**, a **code implementation snippet**, or an **ADR PDF** ready for repository upload.



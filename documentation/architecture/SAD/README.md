A **SAD** is a **Software Architecture Document** (also known as a **Component Architecture** document in Fresenius terminology).
It describes *how a specific component or service is designed, structured, and implemented*, including diagrams, runtime flows, API contracts, and architectural decisions.

Below is the clear, correct explanation **as used in TOGAF, SAFe, and specifically in the Fresenius ADM – Way of Working v3.0**.

---

# ✅ **What is a SAD?**

A **SAD (Software Architecture Document)** is a **component-level architecture document** that describes the internal design of a single software component or microservice.

In the **Fresenius architecture hierarchy**, it corresponds to the **Component Architecture** level.
It is the **lowest-level architectural document that must be ADR-approved** before production deployment.

In the ADM Way of Working it is called:

> **Component Architecture (SAD/SDD)**
> *Format: Markdown (MD) in Azure DevOps, PDF generated to Architecture Repository*
> *Approval: ADR*

📌 In short:
A **PAD describes the whole product**.
A **SAD describes one component of that product**.

---

# 🎯 **Purpose of a SAD**

A SAD provides detailed architecture for a single component, including:

### Functionality

What the component does and why it exists.

### Technical design

Class diagrams, APIs, interfaces, events, queues, etc.

### Runtime behaviour

Sequence diagrams, data flows, event flows.

### Deployment topology

Where it runs (container, app service, edge device), networking, configuration.

### Compliance

Security, privacy, logging, monitoring.

### Architectural alignment

Standards, directives, dependencies, and references to ADRs.

---

# 🔧 **Structure of a typical SAD**

Fresenius uses a standard template (MD in ADO).
A typical SAD contains:

1. **Introduction & Goals**
2. **Referenced Documents & ADRs**
3. **Context Diagram**
4. **Building Block View / Component Structure**
5. **Interfaces & API Contracts**
6. **Runtime View (Sequence / Data Flows)**
7. **Deployment View**
8. **Security & Compliance**
9. **Risks & Technical Debt**
10. **Appendix (DTOs, endpoints, schemas, events)**

It's more detailed than a PAD and much more practical/technical.

---

# 🕒 **When are SADs created?**

According to the **ADM – Way of Working v3.0** (Fresenius), SADs are created at a specific point in the lifecycle:

---

## **They are created AFTER a PAD is approved (start of development)**

…and BEFORE a component can be deployed to production.

### 🔹 **Timeline (simplified)**

**PAD (Product Architecture) approved via ADR**
→ Development begins
→ Components are implemented
→ **SAD (Component Architecture) created for each component**
→ SAD approved via ADR
→ Production deployment allowed

This is stated explicitly in the document:

> **PI Planning Requirements:**
> *Start of Development – ADR approved PAD*
> *Production Deployment – ADR approved Component Architecture (SAD/SDD)*

Meaning:

* You cannot start development without a PAD.
* You cannot deploy a component to production without a SAD.

---

# 🔍 **Why SADs exist**

Because every microservice or architectural component needs a **detailed, governed** design that matches the product-level architecture and target enterprise architecture.

SADs ensure:

* Traceability to the PAD
* Compliance with Standards & Directives
* Consistency across teams
* Proper knowledge transfer
* Review by architects, cybersecurity, data privacy, etc.

Essentially, they prevent “rogue” or undocumented components from entering production.

---

# 🧩 **Relation to other documents**

| Document                         | Scope                         | Format   | Approval       |
| -------------------------------- | ----------------------------- | -------- | -------------- |
| **Synergy / System of Systems**  | Enterprise-wide architecture  | Word     | ATGB           |
| **System**                       | ART-level system architecture | Word     | ADR            |
| **PAD (Product Architecture)**   | Whole product                 | Word     | ADR            |
| **SAD (Component Architecture)** | One component                 | MD + PDF | ADR            |
| **SDD (Software Design Doc)**    | Optional lower-level dev doc  | MD       | ADR (optional) |

---

# 📝 **Example: When you would create a SAD**

You're building the **Interview Assistant Application**:

### PAD:

Defines the whole application → STT pipeline, LLM service, UI overlay, backend API.

### SADs needed for components such as:

* **LLM Integration Service**
* **Audio Transcription Gateway**
* **Real-Time Question Detection Service**
* **Admin/Telemetry API**

Each gets its own SAD.

---

# 📌 **One-sentence summary**

A **SAD is a detailed architecture document for a single component**, created **after PAD approval** and **required before production deployment**, describing design, runtime behavior, interfaces, and compliance.

---

If you'd like, I can now generate:

✅ A **template SAD** (MD format, ready for ADO)
or
✅ A SAD specifically for your **LLM Integration Service** (fully filled, review-ready)

Which one do you want?



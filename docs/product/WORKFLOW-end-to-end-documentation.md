# RecruitOps — End-to-End Workflow Documentation

> **Document Version**: 1.0.0  
> **Scope**: Complete Business & Technical Workflows for RecruitOps v1.0 Production Release

---

## 1. Primary Recruitment Lifecycle Workflow

The primary end-to-end recruitment lifecycle spans 5 distinct stages across 4 user roles (Hiring Manager, Approvers, Recruiter, Candidate, Panel Interviewer):

```mermaid
sequenceDiagram
    autonumber
    actor HM as Hiring Manager
    actor APP as Approver Chain
    actor REC as Recruiter
    actor CAN as Candidate
    actor PAN as Panel Interviewer

    %% Stage 1: Requisition & Approval
    rect rgb(240, 244, 255)
    Note over HM,APP: Stage 1: Requisition & Approval Governance
    HM->>REC: Create Job Requisition (Draft)
    HM->>APP: Submit Requisition for Approval
    APP->>APP: Evaluate Sequential Steps & Threshold Rules
    alt Approved
        APP->>REC: Requisition Approved
    else Rejected
        APP->>HM: Requisition Rejected (with comments)
        HM->>HM: Revise Details (Return to Draft)
        HM->>APP: Resubmit Requisition (Round 2 restarts at Step 1)
    end
    end

    %% Stage 2: Posting & Application
    rect rgb(245, 255, 245)
    Note over REC,CAN: Stage 2: Posting & Public Application
    REC->>REC: Create Job Posting from Approved Requisition
    REC->>CAN: Publish Posting (Unguessable Token Link)
    CAN->>REC: Submit Candidate Application & Upload Resume
    end

    %% Stage 3: CV Parsing & AI Profiling
    rect rgb(255, 250, 240)
    Note over REC: Stage 3: CV Parsing, Search & AI Profiling
    REC->>REC: In-process OCR Text Extraction (PDF/DOCX)
    REC->>REC: Key-Gated AI Skill Extraction & Bilingual Summary
    REC->>REC: Fast Fuzzy Search via Trigram Index (pg_trgm)
    end

    %% Stage 4: Interview & Blind Evaluation
    rect rgb(255, 240, 250)
    Note over REC,PAN: Stage 4: Interview & Blind Panel Evaluation
    REC->>PAN: Schedule Interview & Send iCal Email Invite
    PAN->>PAN: Conduct Interview & Submit Scorecard (Blind)
    Note over PAN: Scores hidden until all scorecards submitted
    PAN->>REC: Transition to Debrief & Discuss via Threaded Notes (@mentions)
    end

    %% Stage 5: Offer & Hired
    rect rgb(240, 255, 255)
    Note over REC,CAN: Stage 5: Offer & Hire
    REC->>CAN: Extend Offer Letter
    CAN->>REC: Accept Offer
    REC->>REC: Mark Candidate as Hired & Update Requisition Headcount
    end
```

---

## 2. Requisition Revise & Resubmit Workflow

When a requisition is rejected by any approver in the sequential chain, it follows a strict non-destructive revision workflow ([PROJECT.md](file:///c:/Users/Min%20Arkar%20Soe/Desktop/Freelance_Project/RecruitOps/PROJECT.md)):

```mermaid
flowchart TD
    A[Hiring Manager creates Requisition] --> B[Submit for Approval]
    B --> C{Sequential Approvers}
    C -->|Step 1 Approved| D{Step 2 Approver}
    C -->|Step 1 Rejected| E[Requisition Status = Rejected]
    D -->|Step 2 Approved| F[Requisition Status = Approved]
    D -->|Step 2 Rejected| E
    
    E --> G[Requester edits details]
    G --> H[Status resets to Draft & Round incremented to Round 2]
    H --> B
    
    style E fill:#ffe6e6,stroke:#ff0000,stroke-width:2px
    style F fill:#e6ffe6,stroke:#00aa00,stroke-width:2px
    style H fill:#fff0e6,stroke:#ff6600,stroke-width:2px
```

---

## 3. Blind Panel Evaluation Workflow

To eliminate interviewer bias during the assessment stage, scorecards enforce strict blind evaluation rules:

```mermaid
flowchart LR
    A[Interview Scheduled] --> B[Panel Member 1 Scores Candidate]
    A --> C[Panel Member 2 Scores Candidate]
    
    B --> D[Scorecard 1 Saved - Blind State]
    C --> E[Scorecard 2 Saved - Blind State]
    
    D --> F{Are all panel scorecards submitted?}
    E --> F
    
    F -->|No| G[Scores hidden from other interviewers]
    F -->|Yes| H[Unblind Scores & Enable Panel Debrief]
    H --> I[Threaded Notes & @Mentions Enabled]
    
    style G fill:#fff3cd,stroke:#ffc107,stroke-width:2px
    style H fill:#d1e7dd,stroke:#0f5132,stroke-width:2px
```

---

## 4. Production Container Infrastructure Topology

The production container infrastructure ([docker-compose.prod.yml](file:///c:/Users/Min%20Arkar%20Soe/Desktop/Freelance_Project/RecruitOps/docker-compose.prod.yml)) enforces reverse proxy traffic routing and dropped direct host ports for zero-trust security:

```mermaid
graph TD
    Client[Browser / Public Client] -->|HTTPS Port 443| Nginx[Nginx Reverse Proxy Container]
    
    subgraph Isolated Production Network
        Nginx -->|/api/*| API[Backend .NET 10 API Container]
        Nginx -->|/jobs/*| Next[Frontend Public Next.js Container]
        Nginx -->|/*| SPA[Frontend Internal Vite SPA Container]
        
        API -->|Port 5432| DB[(PostgreSQL 17 Container)]
        API -->|Port 9000| MinIO[(MinIO / R2 Object Storage Container)]
    end

    style Nginx fill:#d4edda,stroke:#28a745,stroke-width:2px
    style API fill:#cce5ff,stroke:#004085,stroke-width:2px
    style DB fill:#e2e3e5,stroke:#383d41,stroke-width:2px
```

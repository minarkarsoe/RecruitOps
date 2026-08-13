# RecruitOps Hardware & Server Sizing Guide

> **Scope**: Hardware sizing recommendations for per-company single-tenant installations (ADR-0004 & ADR-0005).

---

## 1. Hardware Sizing Matrix

Server specifications scale with company headcount, volume of active requisitions, and daily CV ingestion throughput:

| Company Tier | Employee Count | Active Requisitions / Mo | Daily CV Ingest | Recommended vCPU | Recommended RAM | Initial SSD Storage |
|---|---|---|---|---|---|---|
| **Small Tier** | < 50 staff | < 10 requisitions | < 50 CVs / day | 2 vCPU | 4 GB | 50 GB NVMe SSD |
| **Medium Tier** | 50 – 500 staff | 10 – 50 requisitions | 50 – 300 CVs / day | 4 vCPU | 8 GB | 150 GB NVMe SSD |
| **Enterprise Tier** | > 500 staff | > 50 requisitions | > 300 CVs / day | 8 vCPU | 16 GB | 500 GB NVMe SSD |

---

## 2. Resource Allocation Breakdown

### 2.1 PostgreSQL Database (Postgres 17 + `pg_trgm`)
- **Shared Buffers**: Set to 25% of system RAM (e.g. 2 GB for Medium Tier).
- **Storage Growth**: ~10 MB per 1,000 candidates (including trigram full-text search indexes).

### 2.2 Local CV Text Extraction Load
- PDF and DOCX text parsing runs locally in-process with zero network latency.
- CPU spikes up to 80% during bulk upload batches (50+ CVs). Medium and Enterprise tiers should size vCPU accordingly.

### 2.3 Object Storage (MinIO / Cloudflare R2)
- Attachment Storage: ~500 KB per uploaded candidate resume.
- 10,000 candidate profiles require ~5 GB storage for original resume documents.

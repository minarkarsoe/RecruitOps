// Shared domain types — mirror backend enums (RecruitOps.Domain.Enums).
export type PipelineStatus =
  | 'Sourced' | 'Shortlisted' | 'SentToClient' | 'Interview' | 'Placed' | 'Rejected';
export type ClientFeedback = 'Accepted' | 'NeedMoreInfo' | 'Rejected';
export type ClientTier = 'Gold' | 'Silver' | 'Bronze';
export type ContractStatus = 'Active' | 'ExpiringSoon' | 'Expired';
export type JobStatus = 'Draft' | 'Live' | 'Closed';
export type UserRole = 'Admin' | 'SeniorRecruiter' | 'JuniorRecruiter' | 'Client';

export interface Client { id: string; tenantId: string; tier: ClientTier; }
export interface Job { id: string; tenantId: string; clientId: string; status: JobStatus; }
export interface Candidate { id: string; tenantId: string; }
export interface Application {
  id: string; jobId: string; candidateId: string;
  status: PipelineStatus; clientFeedback?: ClientFeedback;
}

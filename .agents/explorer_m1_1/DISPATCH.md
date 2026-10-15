## 2026-08-11T09:01:39Z
<USER_REQUEST>
You are explorer_m1_1. Your working directory is c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_m1_1.
Read ORIGINAL_REQUEST.md at c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\ORIGINAL_REQUEST.md and PROJECT.md at c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\PROJECT.md.

Task: Provide the precise technical blueprint for Search DTOs, ISearchService interface, and SearchService implementation in RecruitOps.Application and RecruitOps.Infrastructure.
Analyze:
1. Exact DTO definitions to create in backend/src/Application/DTOs/Search/SearchDtos.cs (SearchResultItemDto, CategoryCountsDto, SearchResponseDto, SearchQueryParameters).
2. Interface definition backend/src/Application/Interfaces/ISearchService.cs.
3. SearchService implementation details in backend/src/Infrastructure/Services/SearchService.cs:
   - Query input normalization using IMyanmarScriptNormalizer.
   - Text matching on Candidate (FullName, Email, Phone, ResumeExtractedText, CoverNote, CustomFieldsJson), JobPosting (Title, Description, Location, ApplicationFormFieldsJson), Requisition (Title, JobDescription).
   - Relevance score calculation (0.0 to 100.0) based on match field priority.
   - Context snippet extraction (~150-200 chars) with <mark> highlighting around match terms.
   - Pagination and category filtering.

Write your report to c:\Users\Min Arkar Soe\Desktop\Freelance_Project\RecruitOps\.agents\explorer_m1_1\analysis.md and handoff.md.
Send a message back to parent with summary and file path.
</USER_REQUEST>

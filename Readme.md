# Proof of Concept Prompt: Business Travel Request Workflow

## Goal
Design and outline a simplified workflow system to validate core architecture and integrations. 
This prompt is for **context and planning only** — do not start coding yet.

## Workflow Steps
1. **Employee Request**
   - Employee submits a business travel request (destination, dates, purpose).
   - Option to upload supporting documents (invitation, budget).
   - Documents stored in Azure Storage Account.

2. **Manager Review**
   - Direct manager receives the request.
   - Possible actions:
     - Approve → request moves to travel assignment.
     - Reject → workflow ends.
     - Return for more information → employee updates and resubmits.

3. **Travel Assignment**
   - Approved requests are recorded in Azure SQL Database.
   - Linked documents remain in Storage.

4. **Daily Report**
   - An Azure Function runs once per day.
   - Sends each manager a report of pending requests awaiting approval.

## Technical Components
- **Web App (.NET + Azure SQL)**: interface for employees and managers, persistence of requests and states.
- **Azure Storage Account**: storage for uploaded documents.
- **Workflow Orchestration**: Power Automate (initial) or Logic Apps (future integration).
- **Azure Function**: scheduled job for daily pending approval reports.

## Success Criteria
- Requests flow correctly through states (Pending, Approved, Rejected, Returned).
- Documents are uploaded and linked to requests.
- Managers receive daily pending reports.
- Architecture remains modular and ready for future SAP/Ariba integration.

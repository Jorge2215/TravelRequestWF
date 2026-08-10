Functional Specification – Business Travel Request Workflow (Proof of Concept)

1. Purpose
The application will manage business travel requests submitted by employees and reviewed by their direct managers. The system will allow approvals, rejections, and requests for additional information, while storing supporting documents and generating daily pending reports.

2. Actors
- Employee: Submits travel requests and uploads supporting documents.
- Manager: Reviews requests, approves, rejects, or returns them for clarification.
- System: Records approved requests, stores documents, and sends daily pending reports.

3. Functional Requirements

3.1 Travel Request Submission
- Employee can create a new travel request with:
  - Destination
  - Start and end dates
  - Purpose of travel
- Employee can upload one or more supporting documents.
- Request is saved with status = Pending.

3.2 Manager Review
- Manager can view all pending requests assigned to them.
- Manager actions:
  - Approve → request status changes to Approved.
  - Reject → request status changes to Rejected.
  - Return for more information → request status changes to Returned, employee receives notification.

3.3 Travel Assignment
- Approved requests are recorded in the database as Assigned Travel.
- Linked documents remain stored in Azure Storage.

3.4 Notifications and Reports
- Employee receives notification when request is returned or rejected.
- Manager receives notification when employee resubmits a returned request.
- A scheduled process (Azure Function) sends managers a daily report listing all pending requests.

4. Non-Functional Requirements
- Usability: Simple web interface for employees and managers.
- Security: Role-based access (Employee vs Manager).
- Scalability: Architecture prepared for future integration with SAP/Ariba.
- Reliability: Daily report must run consistently without manual intervention.

5. Data Requirements
- Travel Requests Table: ID, Employee, Destination, Dates, Purpose, Status, ManagerID.
- Documents Storage: Linked to Request ID, stored in Azure Storage Account.
- Audit Log: Records actions (submission, approval, rejection, return).

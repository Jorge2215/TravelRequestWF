Cloud Architecture Document – Business Travel Request Workflow (Proof of Concept)

1. Overview
The application is designed as a modular cloud solution on Microsoft Azure. It supports business travel requests, manager approvals, document storage, and automated reporting. The architecture emphasizes scalability, security, and readiness for future SAP/Ariba integration.

2. Components
- Web Application (.NET on Azure App Service)
  - Provides UI for employees and managers.
  - Handles authentication and role-based access.
  - Connects to Azure SQL Database for persistence.
- Azure SQL Database
  - Stores travel requests, statuses, and audit logs.
  - Ensures relational consistency and reporting capabilities.
- Azure Storage Account (Blob Storage)
  - Stores uploaded documents linked to requests.
  - Provides secure, scalable storage with metadata association.
- Workflow Orchestration
  - Power Automate (initial) for request routing and notifications.
  - Logic Apps (future) for advanced integration with SAP/Ariba.
- Azure Function (Timer Trigger)
  - Executes daily.
  - Queries pending requests and sends email reports to managers.
- Azure Active Directory (AAD)
  - Provides authentication and role-based authorization (Employee vs Manager).

3. Data Flow
1. Employee submits request via Web App → stored in SQL + documents in Storage.
2. Manager reviews request via Web App → actions update SQL status.
3. Workflow engine (Power Automate/Logic Apps) manages notifications.
4. Azure Function runs daily → queries SQL → sends pending report emails.

4. Security Considerations
- Role-based access enforced via AAD.
- Documents stored with private access, linked only through application logic.
- Audit logs maintained for compliance.

5. Architecture Diagram (Conceptual)
See `.squad/files/architecture-diagram.png` (copied from user attachment) for the conceptual diagram (Spanish labels): Empleado/Gerente → Web App .NET (Azure AD Autenticación) → Motor de Workflow (Power Automate / Logic Apps) → Notificación Diaria; Web App also connects to Base de Datos (Azure SQL) and Almacenamiento de Documentos (Azure Storage/Blobs); Aprobación/Rechazo/Devolver para Más Información route to Reporte de Pendientes a Gerentes; Logs de Auditoría feed back from Storage to SQL.

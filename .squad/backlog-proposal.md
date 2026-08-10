# Product Backlog Proposal — Epics & User Stories (Business Travel Request Workflow, PoC)

## Épica 1: Gestión de Solicitudes de Viaje
**Objetivo:** Permitir que los empleados creen y gestionen sus solicitudes de viaje.

| ID | User Story | Criterios de Aceptación |
|----|-------------|--------------------------|
| US 01 | Como empleado, quiero crear una solicitud de viaje indicando destino, fechas y motivo, para iniciar el proceso de aprobación. | Se puede ingresar destino, fechas y motivo. La solicitud se guarda con estado "Pendiente". |
| US 02 | Como empleado, quiero adjuntar documentos (invitaciones, presupuestos) a mi solicitud, para respaldar la información. | Se pueden subir uno o más archivos. Los documentos se almacenan en Azure Storage y se vinculan al ID de la solicitud. |
| US 03 | Como empleado, quiero visualizar el estado de mis solicitudes, para saber si fueron aprobadas, rechazadas o devueltas. | Se muestra listado con estado actual. Se actualiza automáticamente tras cada acción del gerente. |

## Épica 2: Revisión y Aprobación por el Gerente
**Objetivo:** Permitir que el gerente revise y gestione las solicitudes de su equipo.

| ID | User Story | Criterios de Aceptación |
|----|-------------|--------------------------|
| US 04 | Como gerente, quiero ver todas las solicitudes pendientes de mi equipo, para decidir sobre ellas. | Se muestra listado filtrado por estado "Pendiente". |
| US 05 | Como gerente, quiero aprobar una solicitud, para autorizar el viaje. | Al aprobar, el estado cambia a "Aprobado". Se registra fecha y usuario aprobador. |
| US 06 | Como gerente, quiero rechazar una solicitud, para denegar el viaje. | Al rechazar, el estado cambia a "Rechazado". Se notifica al empleado. |
| US 07 | Como gerente, quiero devolver una solicitud solicitando más información, para que el empleado la complete antes de aprobarla. | Al devolver, el estado cambia a "Devuelto". Se envía mensaje al empleado con observaciones. |

## Épica 3: Registro y Reportes Automáticos
**Objetivo:** Registrar viajes aprobados y generar reportes automáticos de pendientes.

| ID | User Story | Criterios de Aceptación |
|----|-------------|--------------------------|
| US 08 | Como sistema, quiero registrar automáticamente las solicitudes aprobadas como viajes asignados, para mantener trazabilidad. | La solicitud queda con estado "Aprobado" (sin tabla separada). Se mantienen vinculados los documentos y datos del empleado. |
| US 09 | Como sistema, quiero enviar diariamente un reporte de solicitudes pendientes a cada gerente, para mantener el flujo activo. | Azure Function ejecuta job diario. Se envía correo con listado de pendientes. |

## Épica 4: Seguridad y Roles
**Objetivo:** Garantizar acceso seguro y diferenciado por perfil.

| ID | User Story | Criterios de Aceptación |
|----|-------------|--------------------------|
| US 10 | Como usuario, quiero iniciar sesión con mi cuenta corporativa, para acceder de forma segura. | Autenticación mediante ASP.NET Identity (cuentas locales, seed hardcodeado para el PoC). Roles asignados (Empleado / Gerente). |
| US 11 | Como sistema, quiero restringir las acciones según el rol del usuario, para evitar accesos indebidos. | Empleado solo puede crear y ver sus solicitudes. Gerente solo puede revisar las de su equipo. |

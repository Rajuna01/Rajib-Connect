# Rajib Connect Desktop CRM — Complete API Client

A Windows desktop client built with Python, Tkinter and Requests. It connects to the existing Rajib Connect ASP.NET Core API using JWT bearer authentication. The ASP.NET Core server remains authoritative for validation, ownership and authorization.

## Included desktop functionality

- JWT login and automatic return to login after an API 401 response.
- Modern blue-and-white desktop interface and role-aware navigation.
- Dashboard analytics for Customers, Leads and Opportunities, including weighted active pipeline.
- Customers: list, search, view, create, update and delete (delete subject to server roles and related-record rules).
- Leads: list, search/filter, view, create, update and delete (delete subject to server roles, ownership and related-record rules).
- Opportunities: list, search, view, create, update and delete; includes stage, amount, probability, weighted value, close date, customer/lead references and assignment.
- Follow-ups: list, search, view, create, update, delete (Admin/Manager), mark completed and mark missed.
- Activities: list, search, view, create, update, delete (Admin/Manager), mark completed and cancel.
- Audit Logs: read-only, Admin-only list and detail view using the API's paged response.
- CSV export for the currently loaded module records.
- Server-side role and ownership enforcement; the client does not bypass API restrictions.

## Backend capabilities not exposed as REST APIs

The supplied ASP.NET Core project does not include JWT REST endpoints for User Management, Lead Conversion or Reports. Therefore this desktop client does not fabricate those operations or write directly to the SQLite database. Those functions remain available through the existing ASP.NET Core MVC website. Audit Logs are read-only as intended.

## Requirements

- Windows 10/11
- Python 3.10+
- Existing Rajib Connect ASP.NET Core API running
- Tkinter/Tcl-Tk component installed with Python

## Setup

1. Extract this folder to `C:\RajibConnect_Desktop` (or another folder).
2. Double-click `setup.bat` once.
3. Double-click `run.bat` to launch.
4. If your Python installation is not under `C:\Users\Admin\AppData\Local\Programs\Python\Python313`, edit the `TCL_LIBRARY` and `TK_LIBRARY` paths in `run.bat` to match your Python installation. These paths are only needed for installations with Tcl/Tk path discovery issues.

## Login

Start the ASP.NET Core project first. Use the local API URL `http://localhost:5184` and your existing CRM username/email and password.

Plain HTTP is permitted only for localhost development. Use HTTPS for remote deployments.

## Role and assignment notes

- Admin and Manager can perform deletes where the API permits them.
- Sales Executives see only records allowed by the API and cannot change ownership.
- Admin/Manager assignment fields require the Identity user ID of an active Sales Executive, not an email address or username.
- Opportunity, Follow-up and Activity forms accept Customer ID and/or Lead ID references. At least one accessible reference is required by the backend.
- Follow-up creation always starts with Planned status and cannot use a past date.
- Activity creation starts with Planned status. Completion/cancellation actions use the API's dedicated endpoints.
- Opportunity amount must be greater than zero; probability must be 0–100; active opportunities cannot have a past expected close date.

## API endpoints used

- `POST /api/auth/login`
- `/api/customers`
- `/api/leads`
- `/api/opportunities`
- `/api/followups` including `/{id}/complete` and `/{id}/miss`
- `/api/activities` including `/{id}/complete` and `/{id}/cancel`
- `/api/auditlogs` (Admin only; paginated GET)

## Safety

The client does not store passwords or JWTs on disk. It keeps the access token in memory for the active session. It does not connect directly to or modify the SQLite database.

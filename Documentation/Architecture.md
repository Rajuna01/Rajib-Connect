\# Rajib Connect – System Architecture



\## 1. Introduction



Rajib Connect is a Customer Relationship Management (CRM) system consisting of a web application and a separate desktop application. The system is designed to support customer management, lead management, sales opportunities, follow-ups, activities, and reporting.



The web application and backend are developed using ASP.NET Core MVC, while the desktop application is developed using Python and Tkinter. REST APIs enable communication between the desktop client and the backend.



\## 2. Architectural Overview



The system follows a layered application architecture.



The major components are:



1\. Presentation Layer

2\. Application and API Layer

3\. Authentication and Authorization Layer

4\. Data Access Layer

5\. Database Layer



\## 3. Presentation Layer



\### 3.1 Web Application



The web interface is developed using ASP.NET Core MVC. It provides the primary interface for interacting with CRM functionality.



Its responsibilities include:



\- Displaying the CRM dashboard.

\- Managing customers and leads.

\- Managing sales opportunities.

\- Managing follow-ups and activities.

\- Providing reports and audit-log views.

\- Supporting role-based access to application functionality.



\### 3.2 Desktop Application



The desktop client is developed using Python and Tkinter.



It communicates with the backend through REST API endpoints.



Its responsibilities include:



\- Providing a desktop interface for CRM operations.

\- Authenticating users through the backend login API.

\- Sending authenticated requests using JWT bearer tokens.

\- Managing supported customer and lead operations.

\- Accessing supported opportunity, follow-up, and activity functionality.

\- Providing dashboard information and CSV export functionality.



\## 4. Application and API Layer



The backend is developed using ASP.NET Core.



It contains two principal interaction mechanisms:



\### 4.1 MVC Controllers



MVC controllers handle browser-based requests and coordinate operations between the presentation layer and application services.



\### 4.2 REST API Controllers



REST API controllers provide endpoints for the desktop client.



The API layer handles requests involving authentication, customers, leads, opportunities, follow-ups, activities, and audit logs.



\## 5. Authentication and Authorization



The system uses ASP.NET Core Identity for user authentication and role management.



JWT-based authentication is used for authenticated API communication.



The desktop client obtains a token through the login API and includes the token in subsequent authenticated requests.



Role-based authorization controls access to protected functionality. Record ownership restrictions are enforced by the backend.



\## 6. Data Access Layer



Entity Framework Core provides object-relational mapping between application models and the database.



The data layer includes:



\- Application database context.

\- Entity models.

\- Database migrations.

\- Database schema management.



\## 7. Database Layer



SQLite is used as the database in the current development environment.



The database stores application information, including:



\- User information.

\- Customer records.

\- Lead records.

\- Sales opportunities.

\- Follow-up information.

\- Activity information.

\- Audit logs.



The local database file is excluded from the public Git repository.



\## 8. Request Processing Workflow



A typical desktop-client request follows these steps:



1\. The user opens the Python desktop application.

2\. The user submits login credentials.

3\. The desktop client sends an authentication request to the backend.

4\. The backend validates the credentials.

5\. The backend returns an authentication token when login succeeds.

6\. The desktop client stores the token for authenticated API requests.

7\. The client sends subsequent requests with the JWT bearer token.

8\. The backend validates authentication and authorization.

9\. The controller processes the requested operation.

10\. Entity Framework Core interacts with SQLite.

11\. The backend returns a response to the desktop client.

12\. The desktop interface displays the result.



\## 9. Technology Stack



| Layer | Technology |

|---|---|

| Web application | ASP.NET Core MVC |

| Backend | C# |

| API | ASP.NET Core Web API |

| Desktop application | Python, Tkinter |

| Authentication | ASP.NET Core Identity |

| API security | JWT |

| ORM | Entity Framework Core |

| Database | SQLite |

| Frontend | HTML, CSS, JavaScript, Bootstrap |

| Version control | Git, GitHub |



\## 10. Security Considerations



The system uses authentication and role-based authorization to protect application functionality.



The desktop client uses JWT bearer tokens for API requests.



The backend is responsible for validating requests and enforcing authorization and record ownership restrictions.



Production deployments should use HTTPS and secure configuration providers for sensitive values.



\## 11. Conclusion



Rajib Connect combines a browser-based CRM application with a Python desktop client through a common ASP.NET Core backend.



The architecture provides a centralized approach to CRM data management while supporting multiple user interfaces and authenticated API communication.




\# Rajib Connect â€“ Customer Relationship Management System



\*\*Rajib Connect\*\* is a CRM application developed using ASP.NET Core MVC and Python. It provides a centralized platform for managing customers, leads, sales opportunities, follow-ups, activities, and CRM operations.



The project includes a web-based CRM application and a separate Python desktop client connected through REST APIs.



\## Project Overview



Rajib Connect consists of two main components:



1\. \*\*Web Application and Backend:\*\* Developed using ASP.NET Core MVC, Entity Framework Core, ASP.NET Core Identity, JWT authentication, and SQLite.

2\. \*\*Desktop Application:\*\* Developed using Python and Tkinter, communicating with the backend through REST APIs.



\## Key Features



\- User authentication and role-based authorization.

\- Customer management (CRUD operations).

\- Lead management (CRUD operations).

\- Sales opportunity management.

\- Follow-up and activity management.

\- Dashboard and CRM reporting.

\- Audit log management.

\- REST API integration.

\- Python desktop client with JWT-based authentication.

\- CSV export functionality in the desktop application.



\## Technology Stack



| Component | Technologies |

|---|---|

| Backend | C#, ASP.NET Core MVC |

| ORM | Entity Framework Core |

| Authentication | ASP.NET Core Identity, JWT |

| Database | SQLite |

| API | ASP.NET Core Web API |

| Frontend | HTML, CSS, JavaScript, Bootstrap |

| Desktop Client | Python, Tkinter |

| HTTP Communication | Python Requests |

| Version Control | Git and GitHub |



\## Project Structure



```text

Rajib-Connect/

â”‚

â”œâ”€â”€ Backend/

â”‚   â””â”€â”€ AcxiomCRM/

â”‚       â”œâ”€â”€ Authorization/

â”‚       â”œâ”€â”€ Controllers/

â”‚       â”œâ”€â”€ Data/

â”‚       â”œâ”€â”€ Migrations/

â”‚       â”œâ”€â”€ Models/

â”‚       â”œâ”€â”€ Services/

â”‚       â”œâ”€â”€ Views/

â”‚       â”œâ”€â”€ wwwroot/

â”‚       â”œâ”€â”€ AcxiomCRM.csproj

â”‚       â””â”€â”€ Program.cs

â”‚

â”œâ”€â”€ DesktopClient/

â”‚   â””â”€â”€ RajibConnect\_Desktop/

â”‚       â”œâ”€â”€ assets/

â”‚       â”œâ”€â”€ gui\_client.py

â”‚       â”œâ”€â”€ requirements.txt

â”‚       â”œâ”€â”€ setup.bat

â”‚       â”œâ”€â”€ run.bat

â”‚       â””â”€â”€ README.md

â”‚

â”œâ”€â”€ Documentation/

â”œâ”€â”€ Screenshots/

â”œâ”€â”€ .gitignore

â””â”€â”€ README.md

```



\## System Requirements



\### Backend

\- Windows, Linux, or macOS.

\- .NET 10 SDK.

\- SQLite.

\- Visual Studio or Visual Studio Code.



\### Desktop Client

\- Windows.

\- Python 3.13 or a compatible Python version.

\- Tkinter support.

\- Internet/network access to the backend API.



\## Installation and Execution



\### 1. Clone the Repository



```bash

git clone https://github.com/YOUR\_GITHUB\_USERNAME/Rajib-Connect.git

cd Rajib-Connect

```



Replace `YOUR\_GITHUB\_USERNAME` with the GitHub account that hosts this repository.



\### 2. Run the Backend



Open a terminal:



```powershell

cd Backend/AcxiomCRM

dotnet restore

dotnet build

dotnet run

```



Configure the backend's database connection and JWT settings securely before running the application.



The backend API and web application should be accessible at the address displayed by ASP.NET Core when it starts.



\### 3. Run the Desktop Application



Open another terminal:



```powershell

cd DesktopClient/RajibConnect\_Desktop

```



Run the setup script:



```powershell

.\\setup.bat

```



After successful setup, launch the application:



```powershell

.\\run.bat

```



Configure the desktop client's API base URL to match the running backend.



\## Authentication and Authorization



The application uses ASP.NET Core Identity and JWT-based authentication.



Access to CRM functionality is controlled through role-based authorization.



The desktop client authenticates with the backend and uses JWT bearer tokens for API communication.



\## Security Considerations



\- Never commit passwords, JWT signing keys, API tokens, or other credentials.

\- Local database files and development configuration files are excluded from the repository.

\- Configure production secrets through environment variables or an appropriate secure configuration provider.

\- Use HTTPS when deploying the application.

\- Do not publish real customer or business records.



\## Future Enhancements



Potential improvements include:



\- Advanced CRM analytics and visualization.

\- Enhanced reporting and data export.

\- Improved desktop and web synchronization.

\- Cloud deployment.

\- Additional integrations and automation.



\## Project Status



The web application and Python desktop client have been developed and tested in the local development environment.



Deployment and production configuration may require additional setup.



\## License



No open-source license has been assigned to this repository. All rights are reserved by the project owner unless a license is added.



\---



\*\*Rajib Connect â€“ CRM Web Application and Desktop Client\*\*



Developed as a software development project.

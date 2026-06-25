# Nataraki Car Rental: Scheduling and Record Management System

Nataraki Car Rental is a comprehensive Windows-based fleet scheduling, rental transaction, and business management platform designed to streamline vehicle reservations, customer management, maintenance tracking, financial operations, and business analytics for car rental companies.

## 👥 Collaborators

<div align="center">

<table>
  <tr>
    <td align="center">
      <a href="https://github.com/ImNatHarvey">
        <img src="https://github.com/ImNatHarvey.png" width="120px;" />
        <br />
        <b>@ImNatHarvey</b>
      </a>
    </td>

<td align="center">
  <a href="https://github.com/xtyannn">
    <img src="https://github.com/xtyannn.png" width="120px;" />
    <br />
    <b>@xtyannn</b>
  </a>
</td>

<td align="center">
  <a href="https://github.com/uncletylr">
    <img src="https://github.com/uncletylr.png" width="120px;" />
    <br />
    <b>@uncletylr</b>
  </a>
</td>

<td align="center">
  <a href="https://github.com/Ash-289257">
    <img src="https://github.com/Ash-289257.png" width="120px;" />
    <br />
    <b>@Ash-289257</b>
  </a>
</td>

  </tr>
</table>

</div>

---

<img src="https://github.com/user-attachments/assets/df7df998-d06e-4abf-a294-bd2f46a9495a" width="100%" alt="System Screenshot">

![Image2](https://github.com/user-attachments/assets/fefb0db6-284b-4dd5-9187-54af6ec0a485)

![Image4](https://github.com/user-attachments/assets/75353505-a403-4ed4-ae85-10cc9264b5e2)

![Image5](https://github.com/user-attachments/assets/753623c3-5d60-46c9-96bd-1a37ee83ca2c)

![Image5](https://github.com/user-attachments/assets/35f99e48-ab65-4e12-92e1-c1502b0cd9a9)

---

# 🚗 System Overview & Ecosystem

Nataraki Car Rental serves as a centralized business management ecosystem that connects fleet operations, customer records, financial transactions, maintenance tracking, and analytics into a single integrated platform.

## 🏢 Business Operations Ecosystem

The system centralizes the following business processes:

- Vehicle Fleet Management
- Fleet Scheduling
- Rental Transactions
- Customer Management
- Vehicle Maintenance Tracking
- GPS Location Monitoring
- Financial Reporting
- User Management
- Activity Auditing

All operational data is stored within a centralized SQL Server database, ensuring data consistency and preventing scheduling conflicts.

## 🔐 Security & Access Control Ecosystem

The platform implements a Role-Based Access Control (RBAC) system that provides:

- Owner Access
- Administrator Access
- Staff Access
- Custom Roles
- Permission Management
- Activity Monitoring
- Audit Logging

This ensures users only access modules relevant to their responsibilities.

## 📊 Analytics Ecosystem

Business intelligence features provide insights into:

- Revenue Performance
- Fleet Utilization
- Customer Analytics
- Operational Monitoring
- Maintenance Costs
- Rental Trends
- Financial Performance

---

# 🚀 System Features

## 🔑 Authentication & Security

- Secure Login System
- Password Encryption
- Role-Based Access Control
- Custom User Roles
- Password Recovery
- Session Management
- Sensitive Action Verification

## 📈 Overview Dashboard

- Real-Time KPI Cards
- Active Fleet Monitoring
- Customer Metrics
- Revenue Summaries
- Recent Transactions
- Recent Reservations
- Upcoming Schedules

## 📅 Fleet Scheduling

- Visual Monthly Planning Board
- Vehicle Reservation Scheduling
- Maintenance Scheduling
- Color-Coded Status Indicators
- Coding Day Validation
- Reservation Conflict Prevention
- Schedule Archiving

## 💰 Transaction Management

- Rental Transaction Processing
- Walk-In Rental Support
- Reservation-Based Transactions
- Dynamic Rental Cost Calculation
- Partial Payment Management
- Payment History Tracking
- Rental Extension Processing
- Return Inspection Management
- Invoice Generation
- Receipt Generation

## 👥 Customer Management

- Customer Registration
- Customer Profile Management
- Blacklisting System
- Document Storage
- Driver's License Management
- Proof of Billing Storage
- Address Validation
- PSGC Integration

## 🚘 Car Garage

- Fleet Inventory Management
- Vehicle Registration Tracking
- Insurance Monitoring
- Vehicle Status Monitoring
- OR/CR Document Storage
- Vehicle Image Management
- Coding Day Configuration
- Vehicle Archiving

## 🔧 Offsite Maintenance Management

- Maintenance Tracking
- Repair Monitoring
- Offsite Records
- Cost Tracking
- Service Documentation
- Repair Audit Trail
- Follow-Up Management

## 🗺️ Vehicle Tracking

- Interactive Map Visualization
- GPS Coordinate Tracking
- Vehicle Location Monitoring
- Tracking History Records

## 📊 Reports & Analytics

- Revenue Reports
- Fleet Utilization Reports
- Customer Analytics
- Operational Monitoring
- Maintenance Reports
- PDF Exports
- Excel Exports

## 📝 Activity Logs

- User Activity Tracking
- Audit Trails
- Module Monitoring
- CRUD Action Logging
- Searchable History
- Date Filtering

## ⚙️ System Management

- Business Configuration
- Branding & Theme Customization
- User Management
- Role Management
- Permission Management
- System Settings

---

# 🛠️ Technical Architecture

## 🖥️ Presentation Tier (Client Layer)

Provides the desktop user interface and user interactions.

### Technologies

- C# WinForms
- ReaLTaiizor
- FontAwesome.Sharp
- LiveCharts2
- Leaflet.js Integration

### Responsibilities

- Dashboard Rendering
- Data Visualization
- User Interaction
- Form Validation
- Navigation Management

---

## ⚙️ Business Logic Tier

Acts as the core processing layer responsible for enforcing business rules.

### Responsibilities

- Reservation Validation
- Date Overlap Prevention
- Role Authorization
- Rental Processing
- Business Rules Enforcement
- Security Validation

### Technologies

- FluentValidation
- BCrypt.Net

---

## 🗄️ Data Access Tier

Handles communication between the application and database.

### Responsibilities

- Data Retrieval
- Data Persistence
- Query Execution
- Repository Operations

### Technologies

- Dapper
- Microsoft.Data.SqlClient
- FluentMigrator

---

## 🗃️ Database Layer

### Microsoft SQL Server

Stores:

- Vehicles
- Customers
- Transactions
- Payments
- Schedules
- Users
- Permissions
- Activity Logs
- Vehicle Locations
- Notifications
- System Settings

---

## 📊 Architecture Flow

```text
User
 │
 ▼
WinForms Interface
 │
 ▼
Business Logic Layer
 │
 ▼
Repository Layer
 │
 ▼
SQL Server Database
 │
 ├── Fleet Management
 ├── Customers
 ├── Transactions
 ├── Payments
 ├── Scheduling
 ├── Analytics
 └── Activity Logs
```

---

# 🗺️ Development Roadmap: Step-by-Step Guide

## Phase 1: Foundation

### Objective

Build the desktop application architecture.

### Execution

- Initialize WinForms Application
- Configure SQL Server
- Create N-Tier Architecture
- Setup Repository Pattern
- Configure Dependency Injection

---

## Phase 2: Authentication & Security

### Objective

Secure user access.

### Execution

- Login System
- Password Encryption
- Role Management
- RBAC Implementation
- Audit Logging

---

## Phase 3: Core Fleet Management

### Objective

Manage vehicle inventory.

### Execution

- Car Garage Module
- Vehicle Registration
- Status Monitoring
- Document Management
- Compliance Tracking

---

## Phase 4: Scheduling System

### Objective

Prevent booking conflicts and manage reservations.

### Execution

- Fleet Schedule Board
- Date Validation Engine
- Reservation Management
- Maintenance Scheduling
- Coding Day Validation

---

## Phase 5: Transaction Management

### Objective

Handle rental operations.

### Execution

- Rental Processing
- Payment Tracking
- Rental Extensions
- Return Inspections
- Invoice Generation

---

## Phase 6: Customer Management

### Objective

Centralize customer information.

### Execution

- Customer Profiles
- PSGC Integration
- Digital Document Vault
- Blacklist Management

---

## Phase 7: Maintenance & Tracking

### Objective

Track offsite repairs and vehicle locations.

### Execution

- Offsite Module
- Vehicle Tracking
- Maintenance Records
- Repair Cost Monitoring

---

## Phase 8: Analytics & Reporting

### Objective

Provide business intelligence.

### Execution

- Revenue Analytics
- Fleet Performance Metrics
- Customer Analytics
- Export System

---

## Phase 9: System Administration

### Objective

Provide advanced configuration capabilities.

### Execution

- User Management
- Permission Management
- Branding System
- Theme Customization
- System Settings

---

# 📂 Project Structure

```text
Nataraki-Car-Rental/
│
├── Presentation/
│   ├── Forms/
│   ├── Controls/
│   ├── Components/
│   ├── Charts/
│   └── Maps/
│
├── BusinessLogic/
│   ├── Services/
│   ├── Validators/
│   ├── Security/
│   └── Rules/
│
├── DataAccess/
│   ├── Repositories/
│   ├── Migrations/
│   ├── Queries/
│   └── Database/
│
├── Models/
│
├── Reports/
│
├── Resources/
│
├── Documentation/
│
├── SQL/
│
└── README.md
```

---

# ⚙️ Execution and Setup

## 🛠️ Prerequisites

Install:

- Windows 10/11
- .NET Desktop Runtime
- Visual Studio 2022
- SQL Server
- SQL Server Management Studio (SSMS)

---

## 📥 Installation Guide

## 📦 Option 1: Install from Release (Recommended)

For end users who only want to use the system without compiling the source code.

### Step 1: Download Latest Release

Navigate to the repository's Releases page and download the latest version:

```text
Releases → Latest Version → Nataraki-Car-Rental.zip
```

Or:

<p align="center">
  <a href="https://github.com/ImNatHarvey/Nataraki-Car-Rental/releases/tag/2.0.0">
    <img src="https://img.shields.io/badge/Download-Latest_Release-blue?style=for-the-badge" alt="Download Release">
  </a>
</p>

### Step 2: Extract Files

Extract the downloaded ZIP file to your preferred location.

Example:

```text
C:\Nataraki-Car-Rental\
```

### Step 3: Configure Database

Ensure Microsoft SQL Server is installed and running.

Update the application's database connection settings if required.

### Step 4: Launch Application

Open:

```text
NatarakiCarRental.exe
```

The application will automatically initialize required configurations and connect to the configured database.

---

## 🛠️ Option 2: Build from Source (Developers)

### Prerequisites

Install:

- Windows 10/11
- Visual Studio 2022
- .NET Framework / .NET Runtime
- SQL Server
- SQL Server Management Studio (SSMS)

### Step 1: Clone Repository

```bash
git clone https://github.com/ImNatHarvey/Nataraki-Car-Rental.git
cd Nataraki-Car-Rental
```

### Step 2: Open Solution

```text
Open NatarakiCarRental.sln
```

### Step 3: Configure Database

- Install SQL Server
- Create database instance
- Update connection string

### Step 4: Restore Packages

```bash
Restore NuGet Packages
```

### Step 5: Build Solution

```bash
Build Solution
```

### Step 6: Run Application

```bash
Start Debugging (F5)
```

or

```bash
Ctrl + F5
```

to run without debugging.

# 🎯 Vision

Nataraki Car Rental aims to modernize fleet rental operations through centralized scheduling, intelligent transaction management, maintenance tracking, operational analytics, and secure business administration, enabling rental companies to manage their entire operation efficiently from a single desktop platform.

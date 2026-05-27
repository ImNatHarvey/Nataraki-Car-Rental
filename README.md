# 🚗 Nataraki Car Rental - Management System

![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)
![SQL Server](https://img.shields.io/badge/SQL_Server-CC2927?style=for-the-badge&logo=microsoft-sql-server&logoColor=white)

## 📖 Introduction
Nataraki Car Rental is a comprehensive, Windows-based system designed to streamline car rental operations. By transitioning from manual, paper-based workflows to a highly efficient digital ecosystem, this application provides business owners and staff with a centralized platform for scheduling, transaction processing, and record management.

## 🌟 System Overview
The system utilizes a clean N-Tier architecture (Presentation, Business Logic, and Data Access) tailored for a seamless desktop experience. It features Role-Based Access Control (RBAC), dynamic branding, automated database initialization, and a robust set of operational modules designed specifically for fleet management. 

### Core Features
* **📊 Overview (Dashboard):** Real-time monitoring of business metrics, active rentals, pending reservations, and daily revenue.
* **📅 Fleet Schedule:** A visual calendar timeline to manage reservations, ongoing rentals, and maintenance while preventing overlapping schedules.
* **💳 Transactions:** End-to-end processing of walk-in and reserved rentals, including payment tracking, daily rate calculations, and return inspections.
* **👥 Customers:** Comprehensive customer profiles with history tracking and a blacklisting workflow to protect the business.
* **🚘 Car Garage:** Full fleet registry supporting plate number formatting, coding day identification, and vehicle status management.
* **📍 Offsite & Map Tracking:** Tracking for vehicles sent for maintenance/repair, featuring an interactive Leaflet map integration for geographic visualization.
* **📈 Reports & Analytics:** Automated generation of financial, operational, and fleet performance reports with PDF and Excel export capabilities.
* **⚙️ Manage System (RBAC):** Complete administrative control over dynamic system branding (logos/colors), role permissions, and user account creation.

---

## 🛠️ Built With
* **Language:** C#
* **Framework:** .NET 10.0 (Windows Forms)
* **Database:** Microsoft SQL Server
* **Mapping:** Leaflet.js (Embedded HTML)
* **Icons & UI:** FontAwesome.Sharp

---

## 🚀 Getting Started (Setup Guide)

The application features a "zero-config" database setup. You do not need to manually create the database; the system will automatically configure it on its first launch.

### 1. Prerequisites
Before running the system, you must have the following installed on your local machine:
* [**SQL Server Express**](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) - Required for the local database engine.
* [**SQL Server Management Studio (SSMS)**](https://learn.microsoft.com/en-us/ssms/install/install) - Required to view and manage the database structure.

### 2. Installation & Running
Follow these steps to test the application:
1.  **Download the Release:** Go to the official release page:
    [https://github.com/ImNatHarvey/NatarakiCarRental/releases/tag/v1.0.0-beta](https://github.com/ImNatHarvey/NatarakiCarRental/releases/tag/v1.0.0-beta)
2.  **Extract the Files:** Download the `Nataraki-v1.0-Beta.zip` file under the "Assets" section and extract it to a folder on your computer.
3.  **Launch the System:** Open the extracted folder and double-click `NatarakiCarRental.exe`.
    * *Note: During the very first launch, the application might freeze for a few seconds. This is completely normal—the system is automatically building the SQL tables and inserting default data in the background.*

### 3. Default Demo Login
Use the following credentials to access the system with full Owner privileges:
* **Username:** `NatarakiCar`
* **Password:** `Nataraki2026`

# Nataraki Car Rental

Nataraki Car Rental is a desktop WinForms application using a local Service + Repository backend layer with direct SQL Server access through Dapper. It does not use a REST API server.

## Demo setup

Default demo login:

- Username: `NatarakiCar`
- Password: `Nataraki2026`

Default SQL Server name:

- `HARVEY`

Groupmates can override SQL settings without changing source code:

- `NATARAKI_SQL_SERVER`
- `NATARAKI_CONNECTION_STRING`

Demo/bootstrap credentials can also be overridden:

- `NATARAKI_BOOTSTRAP_USERNAME`
- `NATARAKI_BOOTSTRAP_PASSWORD`

Uploads are stored under:

- `%LocalAppData%\Nataraki Car Rental\Uploads`

Philippine address data is downloaded once during startup seeding and then cached locally in:

- `App_Data`

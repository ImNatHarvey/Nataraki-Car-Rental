import os
import re

files_to_fix = [
    r"NatarakiCarRental\Repositories\ActivityLogRepository.cs",
    r"NatarakiCarRental\Repositories\CarRepository.cs",
    r"NatarakiCarRental\Repositories\CustomerRepository.cs",
    r"NatarakiCarRental\Repositories\FleetScheduleRepository.cs",
    r"NatarakiCarRental\Repositories\NotificationRepository.cs",
    r"NatarakiCarRental\Repositories\PermissionRepository.cs",
    r"NatarakiCarRental\Repositories\RoleRepository.cs",
    r"NatarakiCarRental\Repositories\TransactionPaymentRepository.cs",
    r"NatarakiCarRental\Repositories\TransactionRepository.cs",
    r"NatarakiCarRental\Repositories\UserRepository.cs",
    r"NatarakiCarRental\Services\ActivityLogService.cs",
    r"NatarakiCarRental\Services\CustomerService.cs",
    r"NatarakiCarRental\Services\FleetScheduleService.cs",
    r"NatarakiCarRental\Services\NotificationService.cs",
    r"NatarakiCarRental\Services\TransactionService.cs",
    r"NatarakiCarRental\Services\CarService.cs"
]

for file_path in files_to_fix:
    if not os.path.exists(file_path):
        print(f"Skipping {file_path}: File not found")
        continue
    
    with open(file_path, 'r', encoding='utf-8') as f:
        content = f.read()
    
    if "IDbTransaction" in content and "using System.Data;" not in content:
        print(f"Fixing {file_path}")
        # Insert using System.Data; at the top
        new_content = "using System.Data;\n" + content
        with open(file_path, 'w', encoding='utf-8') as f:
            f.write(new_content)
    else:
        print(f"No fix needed for {file_path}")

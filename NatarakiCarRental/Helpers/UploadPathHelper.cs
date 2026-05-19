namespace NatarakiCarRental.Helpers;

public static class UploadPathHelper
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    private static readonly HashSet<string> DocumentExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".pdf"
    };

    public static string? SaveCarFileIfSelected(string? sourcePath, string? existingPath, bool allowPdf)
    {
        return SaveUploadedFileIfSelected(
            sourcePath,
            existingPath,
            AppConstants.CarsUploadFolder,
            allowPdf ? DocumentExtensions : ImageExtensions);
    }

    public static string? SaveCustomerFileIfSelected(string? sourcePath, string? existingPath)
    {
        return SaveUploadedFileIfSelected(
            sourcePath,
            existingPath,
            AppConstants.CustomersUploadFolder,
            DocumentExtensions);
    }

    public static string? SavePaymentReceiptIfSelected(string? sourcePath, string? existingPath)
    {
        return SaveUploadedFileIfSelected(
            sourcePath,
            existingPath,
            AppConstants.PaymentsUploadFolder,
            DocumentExtensions);
    }

    public static string? ResolveCarFilePath(string? storedPath)
    {
        return ResolveExistingFilePath(storedPath, AppConstants.CarsUploadFolder);
    }

    public static string? ResolveCustomerFilePath(string? storedPath)
    {
        return ResolveExistingFilePath(storedPath, AppConstants.CustomersUploadFolder);
    }

    public static string? ResolvePaymentReceiptPath(string? storedPath)
    {
        return ResolveExistingFilePath(storedPath, AppConstants.PaymentsUploadFolder);
    }

    public static void DeleteNewCarUploadIfSaveFailed(string? storedPath, string? previousPath)
    {
        DeleteNewUploadIfSaveFailed(storedPath, previousPath, AppConstants.CarsUploadFolder);
    }

    public static void DeleteNewCustomerUploadIfSaveFailed(string? storedPath, string? previousPath)
    {
        DeleteNewUploadIfSaveFailed(storedPath, previousPath, AppConstants.CustomersUploadFolder);
    }

    public static void DeleteNewPaymentReceiptIfSaveFailed(string? storedPath, string? previousPath)
    {
        DeleteNewUploadIfSaveFailed(storedPath, previousPath, AppConstants.PaymentsUploadFolder);
    }

    private static string? SaveUploadedFileIfSelected(
        string? sourcePath,
        string? existingPath,
        string relativeFolder,
        IReadOnlySet<string> allowedExtensions)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return existingPath;
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected upload file no longer exists.", sourcePath);
        }

        string extension = Path.GetExtension(sourcePath);

        if (!allowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException($"Unsupported file type '{extension}'.");
        }

        string uploadDirectory = GetLocalUploadDirectory(relativeFolder);
        Directory.CreateDirectory(uploadDirectory);

        string fileName = $"{Guid.NewGuid():N}{extension}";
        string destinationPath = Path.Combine(uploadDirectory, fileName);
        File.Copy(sourcePath, destinationPath, overwrite: false);

        return Path.Combine(relativeFolder, fileName);
    }

    private static string? ResolveExistingFilePath(string? storedPath, string relativeFolder)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
        {
            return null;
        }

        if (Path.IsPathRooted(storedPath) && File.Exists(storedPath))
        {
            return storedPath;
        }

        string fileName = Path.GetFileName(storedPath);
        string newLocation = Path.Combine(GetLocalUploadDirectory(relativeFolder), fileName);

        if (File.Exists(newLocation))
        {
            return newLocation;
        }

        string legacyLocation = Path.Combine(AppContext.BaseDirectory, relativeFolder, fileName);

        if (File.Exists(legacyLocation))
        {
            return legacyLocation;
        }

        string relativeToBaseDirectory = Path.Combine(AppContext.BaseDirectory, storedPath);
        return File.Exists(relativeToBaseDirectory) ? relativeToBaseDirectory : null;
    }

    private static void DeleteNewUploadIfSaveFailed(string? storedPath, string? previousPath, string relativeFolder)
    {
        if (string.IsNullOrWhiteSpace(storedPath)
            || string.Equals(storedPath, previousPath, StringComparison.OrdinalIgnoreCase)
            || Path.IsPathRooted(storedPath))
        {
            return;
        }

        string expectedFolder = NormalizeRelativePath(relativeFolder);
        string storedDirectory = NormalizeRelativePath(Path.GetDirectoryName(storedPath) ?? string.Empty);

        if (!string.Equals(storedDirectory, expectedFolder, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string candidatePath = Path.Combine(GetLocalUploadDirectory(relativeFolder), Path.GetFileName(storedPath));

        if (File.Exists(candidatePath))
        {
            File.Delete(candidatePath);
        }
    }

    private static string GetLocalUploadDirectory(string relativeFolder)
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppConstants.ApplicationName,
            relativeFolder);
    }

    private static string NormalizeRelativePath(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Trim(Path.DirectorySeparatorChar);
    }
}

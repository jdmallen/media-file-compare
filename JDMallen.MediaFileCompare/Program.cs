using System.Text.RegularExpressions;
using ExifLib;
using Microsoft.WindowsAPICodePack.Shell;
using Microsoft.WindowsAPICodePack.Shell.PropertySystem;

const string sourceDir = @"D:\gdrive\Google Photos";
const string targetDir = @"J:\Pictures\Device\pixel2xl\DCIM\Camera";
const double sizeMargin = 0.01;
TimeSpan timeMargin = TimeSpan.FromMinutes(1);

HashSet<string?> filesInSource =
	Directory.EnumerateFiles(sourceDir).Select(Path.GetFileName).ToHashSet();
HashSet<string?> filesInTarget =
	Directory.EnumerateFiles(targetDir).Select(Path.GetFileName).ToHashSet();

List<string?> filesInCommon = filesInSource.Intersect(filesInTarget).ToList();

foreach (string? file in filesInCommon)
{
	if (file == null)
	{
		continue;
	}

	string leftFilePath = Path.Combine(sourceDir, file);
	string rightFilePath = Path.Combine(targetDir, file);
	var dateFromFilename = GetDateFromFilename(file);

	// Check if JPEG
	if (Path.GetExtension(file).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
	    || Path.GetExtension(file).Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
	{
		var left = new FileInfo(leftFilePath);
		var right = new FileInfo(rightFilePath);

		using var leftExif = new ExifReader(leftFilePath);
		using var rightExif = new ExifReader(rightFilePath);

		// Check if same date taken
		if (leftExif.GetTagValue<DateTime>(ExifTags.DateTimeDigitized, out var dateLeftTaken)
		    && rightExif.GetTagValue<DateTime>(ExifTags.DateTimeDigitized, out var dateRightTaken)
		    && dateLeftTaken != dateRightTaken)
		{
			// Not same picture
			continue;
		}
		
		// Check if file2's date modified matches filename
		if (TimesWithinMargin(right.LastWriteTime, dateFromFilename, timeMargin)
		    && TimesWithinMargin(left.LastWriteTime, dateFromFilename, timeMargin))
		{
			MarkLargerFileAsReadOnly(left, right);
		}
		else if (TimesWithinMargin(right.LastWriteTime, dateFromFilename, timeMargin)
		         && left.Length == right.Length)
		{
			MarkAsReadOnly(right);
		}
		else if (TimesWithinMargin(right.LastWriteTime, dateFromFilename, timeMargin)
		         && FilesSizeWithinMargin(left, right, sizeMargin))
		{
			MarkAsReadOnly(right);
		}
		else
		{
			// Compare file sizes and mark larger with Read Only file attribute
			MarkLargerFileAsReadOnly(left, right);
		}
	}

	// Check if MP4
	if (Path.GetExtension(file).Equals(".mp4", StringComparison.OrdinalIgnoreCase)
	    || Path.GetExtension(file).Equals(".m4v", StringComparison.OrdinalIgnoreCase))
	{
		var left = new FileInfo(leftFilePath);
		var right = new FileInfo(rightFilePath);
		
		// Check if file2's date modified matches filename
		if (TimesWithinMargin(right.LastWriteTime, dateFromFilename, timeMargin)
		    && TimesWithinMargin(left.LastWriteTime, dateFromFilename, timeMargin))
		{
			MarkLargerFileAsReadOnly(left, right);
		}
		else if (TimesWithinMargin(right.LastWriteTime, dateFromFilename, timeMargin)
		         && left.Length == right.Length)
		{
			MarkAsReadOnly(right);
		}
		else if (TimesWithinMargin(right.LastWriteTime, dateFromFilename, timeMargin)
		         && FilesSizeWithinMargin(left, right, sizeMargin))
		{
			MarkAsReadOnly(right);
		}
		else
		{
			// Compare file sizes and mark larger with Read Only file attribute
			MarkLargerFileAsReadOnly(left, right);
		}
	}
}

Debugger.Break();

return;

static bool TimesWithinMargin(DateTime time1, DateTime time2, TimeSpan margin)
{
	return time1 - time2 <= margin;
}

static DateTime GetDateFromFilename(string filePath)
{
	string filename = Path.GetFileNameWithoutExtension(filePath);
	var matches = Regex.Match(
		filename,
		"(20[0-2][0-9][01][0-9][0-3][0-9]_[0-2][0-9][0-5][0-9][0-5][0-9])");
	const string dateFormat = "yyyyMMdd_HHmmss";
	if (!matches.Success)
	{
		return DateTime.MinValue;
	}

	string dateToParse = matches.Groups[1].Value;

	return DateTime.ParseExact(
		Path.GetFileNameWithoutExtension(dateToParse).Replace("IMG_", ""),
		dateFormat,
		CultureInfo.InvariantCulture);
}

static bool FilesSizeWithinMargin(FileInfo file1, FileInfo file2, double margin)
{
	return Math.Max(file1.Length, (double)file2.Length)
		/ Math.Min(file1.Length, (double)file2.Length)
		- 1
		<= margin;
}

static void MarkLargerFileAsReadOnly(FileInfo file1, FileInfo file2)
{
	MarkAsReadOnly(file1.Length > file2.Length ? file1 : file2);
}

static void MarkAsReadOnly(FileSystemInfo file)
{
	file.Attributes |= FileAttributes.ReadOnly;
}

static DateTime? GetMediaCreatedDate(string filePath)
{
	try
	{
		ShellObject video = ShellObject.FromParsingName(filePath);

		// var dateAccessed = video.Properties.GetProperty(SystemProperties.System.DateAccessed)?.ValueAsObject?.ToString();
		var dateAcquired = video.Properties.GetProperty(SystemProperties.System.DateAcquired)
			?.ValueAsObject?.ToString();

		// var dateArchived = video.Properties.GetProperty(SystemProperties.System.DateArchived)?.ValueAsObject?.ToString();
		// var dateCompleted = video.Properties.GetProperty(SystemProperties.System.DateCompleted)?.ValueAsObject?.ToString();
		// var dateCreated = video.Properties.GetProperty(SystemProperties.System.DateCreated)?.ValueAsObject?.ToString();
		// var dateImported = video.Properties.GetProperty(SystemProperties.System.DateImported)?.ValueAsObject?.ToString();
		var dateModified = video.Properties.GetProperty(SystemProperties.System.DateModified)
			?.ValueAsObject?.ToString();

		return DateTime.Parse(dateAcquired ?? dateModified ?? string.Empty);
	}
	catch (Exception)
	{
		return null;
	}
}

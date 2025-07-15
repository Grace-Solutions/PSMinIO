# Test XML Parsing for Multipart Upload Response
# This script tests the XML parsing logic without modifying the codebase

# Sample XML response from your S3 server
$xmlResponse = @'
<?xml version="1.0" encoding="UTF-8"?>
<InitiateMultipartUploadResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/"><Bucket>xylem</Bucket><Key>Win10ISO.zip</Key><UploadId>MzBlZDdmNjUtZjNjYi00YzhhLThhMWQtNzViYTAyNWIzYjFhLmJiYjBmNzcxLWZiODEtNGFhYy1iYWQwLTk3M2EyNDIwMjRkM3gxNzUyNTQwMTc4NzE4NjczNjY1</UploadId></InitiateMultipartUploadResult>
'@

Write-Output "=== XML Parsing Test ==="
Write-Output "Testing XML response parsing logic"
Write-Output ""

# Parse the XML
$doc = [System.Xml.Linq.XDocument]::Parse($xmlResponse)

Write-Output "1. Basic XML parsing:"
Write-Output "   Root element: $($doc.Root.Name)"
Write-Output "   Root namespace: $($doc.Root.GetDefaultNamespace())"
Write-Output ""

# Test method 1: Without namespace (current broken method)
Write-Output "2. Method 1 - Without namespace (broken):"
$uploadId1 = $doc.Descendants("UploadId") | Select-Object -First 1 | ForEach-Object { $_.Value }
Write-Output "   UploadId found: $($uploadId1 ?? 'NULL')"
Write-Output ""

# Test method 2: With namespace (fixed method)
Write-Output "3. Method 2 - With namespace (fixed):"
$ns = $doc.Root.GetDefaultNamespace()
if ($ns) {
    $uploadId2 = $doc.Descendants([System.Xml.Linq.XName]::Get("UploadId", $ns.NamespaceName)) | Select-Object -First 1 | ForEach-Object { $_.Value }
} else {
    $uploadId2 = $doc.Descendants("UploadId") | Select-Object -First 1 | ForEach-Object { $_.Value }
}
Write-Output "   Namespace: $($ns.NamespaceName)"
Write-Output "   UploadId found: $($uploadId2 ?? 'NULL')"
Write-Output ""

# Test method 3: Alternative approach
Write-Output "4. Method 3 - Alternative approach:"
$uploadId3 = $doc.Descendants() | Where-Object { $_.Name.LocalName -eq "UploadId" } | Select-Object -First 1 | ForEach-Object { $_.Value }
Write-Output "   UploadId found: $($uploadId3 ?? 'NULL')"
Write-Output ""

# Show all elements
Write-Output "5. All elements in the XML:"
$doc.Descendants() | ForEach-Object {
    Write-Output "   Element: $($_.Name.LocalName) = $($_.Value)"
}
Write-Output ""

# Test the exact logic that should work
Write-Output "6. Recommended fix logic:"
$ns = $doc.Root.GetDefaultNamespace()
if ($ns -and $ns.NamespaceName) {
    $uploadIdFinal = $doc.Descendants([System.Xml.Linq.XName]::Get("UploadId", $ns.NamespaceName)) | Select-Object -First 1 | ForEach-Object { $_.Value }
} else {
    $uploadIdFinal = $doc.Descendants("UploadId") | Select-Object -First 1 | ForEach-Object { $_.Value }
}

Write-Output "   Final UploadId: $($uploadIdFinal ?? 'NULL')"
Write-Output "   Success: $($uploadIdFinal -ne $null -and $uploadIdFinal -ne '')"
Write-Output ""

if ($uploadIdFinal) {
    Write-Output "✅ XML parsing would work with namespace handling!"
    Write-Output "   The UploadId '$uploadIdFinal' was successfully extracted."
} else {
    Write-Output "❌ XML parsing still has issues."
}

Write-Output ""
Write-Output "=== Test Complete ==="

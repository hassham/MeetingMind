[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$confirmationPhrase = "DELETE MEETINGMIND LOCAL DATABASE"

Write-Host ""
Write-Warning "This deletes the MeetingMind PostgreSQL Docker volume."
Write-Warning "All local jobs, database metadata, and migration history will be lost."
Write-Host "Files under the repository Storage folder are not deleted."
Write-Host ""

$confirmation = Read-Host "Type '$confirmationPhrase' to continue"
if ($confirmation -cne $confirmationPhrase) {
    Write-Host "Database reset cancelled."
    exit 1
}

Push-Location $repositoryRoot
try {
    docker compose down --volumes
    if ($LASTEXITCODE -ne 0) {
        throw "Docker could not remove the MeetingMind database volume."
    }

    docker compose up -d meetingmind-postgres
    if ($LASTEXITCODE -ne 0) {
        throw "Docker could not start the MeetingMind PostgreSQL service."
    }

    Write-Host "Waiting for PostgreSQL to accept connections..."
    $ready = $false
    foreach ($attempt in 1..30) {
        docker compose exec -T meetingmind-postgres `
            pg_isready -U meetingmind_user -d meetingmind *> $null
        if ($LASTEXITCODE -eq 0) {
            $ready = $true
            break
        }

        Start-Sleep -Seconds 1
    }

    if (-not $ready) {
        throw "PostgreSQL did not become ready. Run 'docker compose logs meetingmind-postgres'."
    }

    Write-Host "Local database reset complete."
    Write-Host "Start the Development API; it will create and migrate the database automatically."
}
finally {
    Pop-Location
}

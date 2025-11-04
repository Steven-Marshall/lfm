# Test SSH Config for All Machines
# Run this after adding the SSH config entries

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Testing SSH Configuration" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

$machines = @(
    @{Name="spark"; Host="192.168.1.29"; User="steven"},
    @{Name="nuc"; Host="nuc"; User="steven"},
    @{Name="pihole"; Host="pihole"; User="pi"},
    @{Name="pihole2"; Host="pihole2"; User="pi"},
    @{Name="pidev"; Host="pidev"; User="pi"},
    @{Name="pitorrent"; Host="pitorrent"; User="pi"}
)

foreach ($machine in $machines) {
    Write-Host "Testing: $($machine.Name)" -ForegroundColor Yellow

    # Test 1: Ping
    Write-Host "  [1/3] Ping test... " -NoNewline
    $ping = Test-Connection -ComputerName $machine.Host -Count 1 -Quiet -ErrorAction SilentlyContinue
    if ($ping) {
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "FAILED" -ForegroundColor Red
        Write-Host "        (Machine may be offline or hostname not resolving)" -ForegroundColor Gray
        Write-Host ""
        continue
    }

    # Test 2: SSH config exists
    Write-Host "  [2/3] SSH config... " -NoNewline
    $sshConfig = Get-Content "$env:USERPROFILE\.ssh\config" -Raw -ErrorAction SilentlyContinue
    if ($sshConfig -match "Host $($machine.Name)\s") {
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "MISSING" -ForegroundColor Red
        Write-Host "        (Add the SSH config entry for this host)" -ForegroundColor Gray
        Write-Host ""
        continue
    }

    # Test 3: SSH connection
    Write-Host "  [3/3] SSH connection... " -NoNewline
    $sshTest = ssh -o ConnectTimeout=5 -o BatchMode=yes $machine.Name "echo 'success'" 2>$null
    if ($sshTest -eq "success") {
        Write-Host "OK" -ForegroundColor Green
    } else {
        Write-Host "FAILED" -ForegroundColor Red
        Write-Host "        (Check SSH keys or run: ssh-copy-id $($machine.User)@$($machine.Host))" -ForegroundColor Gray
    }

    Write-Host ""
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Test Complete!" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. If any pings failed: Check if machine is powered on"
Write-Host "2. If SSH config missing: Add entries to $env:USERPROFILE\.ssh\config"
Write-Host "3. If SSH connection failed: Run 'ssh-keyscan <hostname> >> ~/.ssh/known_hosts'"
Write-Host "   or try connecting manually once: ssh <hostname>"

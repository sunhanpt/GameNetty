$ErrorActionPreference = 'Stop'

# Listener ports for process 1 in Config/Generate/StartConfig/Localhost.
$tcpPorts = @(20001, 30002, 30003, 30004, 30300, 30301, 30302, 30303, 30304)
$udpPorts = @(30002, 30003, 30004, 30301, 30302, 30303, 30304)
$address = [Net.IPAddress]::Loopback
if ($env:ET_BIND_ANY -eq '1') {
    $address = [Net.IPAddress]::Any
}

function Test-ListenerPorts($ports, $socketType, $protocol) {
    foreach ($port in $ports) {
        $socket = [Net.Sockets.Socket]::new([Net.Sockets.AddressFamily]::InterNetwork, $socketType, $protocol)
        try {
            $socket.ExclusiveAddressUse = $true
            $socket.Bind([Net.IPEndPoint]::new($address, $port))
        }
        catch {
            $failure = $_.Exception.GetBaseException()
            if ($failure -isnot [Net.Sockets.SocketException]) { throw }
            [pscustomobject]@{
                Protocol = $protocol
                Port = $port
                ErrorCode = $failure.NativeErrorCode
                Reason = $failure.SocketErrorCode
            }
        }
        finally {
            $socket.Dispose()
        }
    }
}

$failures = @(
    Test-ListenerPorts $tcpPorts ([Net.Sockets.SocketType]::Stream) ([Net.Sockets.ProtocolType]::Tcp)
    Test-ListenerPorts $udpPorts ([Net.Sockets.SocketType]::Dgram) ([Net.Sockets.ProtocolType]::Udp)
)

if ($failures.Count -gt 0) {
    $listeners = netstat.exe -ano
    foreach ($failure in $failures) {
        $protocol = $failure.Protocol.ToString().ToUpperInvariant()
        $pattern = '^\s*' + $protocol + '\s+\S+:' + $failure.Port + '\s'
        Write-Host ("[GameNetty] Cannot bind {0} {1}:{2} ({3}, socket {4})." -f
            $protocol, $address, $failure.Port, $failure.Reason, $failure.ErrorCode)
        foreach ($listener in ($listeners | Select-String -Pattern $pattern)) {
            Write-Host $listener.Line
        }
    }
    if ($failures.Reason -contains [Net.Sockets.SocketError]::AddressAlreadyInUse) {
        Write-Host '[GameNetty] Close the existing server or free the listed ports, then start again.'
    }
    exit 1
}

exit 0

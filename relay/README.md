# Self-hosted relay

GTACoop Remake can connect players without port forwarding by tunnelling the
game and sync ports through a relay. By default it uses the public
`bore.pub` relay. If you would rather not send traffic through a shared
service, run your own relay on a small VPS.

The relay is a `bore` server (the same protocol the launcher's tunnel client
speaks). It never sees your mods, your account or your game data — it only
forwards the two TCP/UDP ports that are already in use by the session.

## Requirements

- A Linux VPS with a public IPv4 address (1 vCPU / 512 MB is plenty).
- Docker with the Compose plugin (`docker compose version`).
- TCP `7835` open in the firewall and in the cloud provider's security group.

## Start it

```bash
cd relay
docker compose up -d
docker compose logs -f
```

The log prints the control port (`7835`). Leave it running; it restarts on
reboot because of `restart: unless-stopped`.

## Use it from the launcher

1. Open the **Sunucum** tab and start your server.
2. In **Relay sunucusu**, replace `bore.pub` with your relay address:
   - `relay.example.com` (control port 7835 is implied), or
   - `relay.example.com:7835` when you mapped a different port.
3. Create the join code as usual. Guests need no configuration: the relay
   endpoint travels inside the join code.

## Notes

- A relay adds latency; prefer a VPS near your players.
- The tunnel is not a VPN. Only the two session ports are forwarded.
- If you rotate the relay address, guests must fetch a fresh join code.
- Bandwidth is roughly `players x game traffic`; the 512 MB tier handles a
  small private lobby comfortably.

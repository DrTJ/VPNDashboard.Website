git clone https://github.com/DrTJ/VPNDashboard.Website.git /tmp/vpn-dashboard
cd /tmp/vpn-dashboard


Publish:
1. dotnet publish src/VPNDashboard.Website/VPNDashboard.Website.csproj -c Release -o publish
2. scp /tmp/vpn-dashboard-release.tar.gz root@5.161.115.89:/tmp/
3. 
cd /tmp/vpn-dashboard
tar xzf /tmp/vpn-dashboard-release.tar.gz
cp -r -f publish/* /opt/vpn-dashboard/
chown -R vpndash:vpndash /opt/vpn-dashboard/
systemctl restart vpn-dashboard







mkdir -p /etc/systemd/system/vpn-dashboard.service.d
cat > /etc/systemd/system/vpn-dashboard.service.d/seed-admin.conf << 'EOF'
[Service]
Environment=VPNDASH_ADMIN_EMAIL=admin
Environment=VPNDASH_ADMIN_PASSWORD=TJ136645
EOF
systemctl daemon-reload
systemctl restart vpn-dashboard

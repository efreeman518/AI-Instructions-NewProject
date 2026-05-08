# Install WSL - Docker - Podman

Using .NET Aspire on Windows development machines requires WSL/Docker running locally. Note we are installing the Docker engine, not Docker Desktop which has licensing restrictions. Podman is an alternative GUI replacement for Docker Desktop.

## Table of Contents

1. [Install WSL2](#1-install-wsl2)
2. [Update WSL2](#2-update-wsl2)
3. [Install Ubuntu (Latest)](#3-install-ubuntu-latest)
4. [Update and Upgrade Ubuntu Packages](#4-update-and-upgrade-ubuntu-packages)
5. [Install Docker Engine in WSL2 Ubuntu](#5-install-docker-engine-in-wsl2-ubuntu)
6. [(Optional) Make Docker Accessible from Windows PowerShell](#6-optional-make-docker-accessible-from-windows-powershell)
   - [Find Your PowerShell Profile Location](#find-your-powershell-profile-location)
   - [Create or Edit Your Profile](#create-or-edit-your-profile)
   - [Add the Docker Alias](#add-the-docker-alias)
   - [Reload Your Profile](#reload-your-profile)
7. [(Optional) Install Podman Desktop as Docker Desktop Replacement](#7-optional-install-podman-desktop-as-docker-desktop-replacement)
   - [Install Podman Desktop](#install-podman-desktop)
   - [Launch Podman Desktop](#launch-podman-desktop)
   - [View Images and Containers](#view-images-and-containers)
   - [Command Line Access](#command-line-access)
- [Appendix: Common Docker Commands](#appendix-common-docker-commands)

---

## 1. Install WSL2

Open PowerShell as Administrator and install WSL:

```powershell
wsl --install
```

Reboot when prompted.

## 2. Update WSL2

After reboot, update WSL2 to the latest version:

```powershell
wsl --update
wsl --set-default-version 2
```

## 3. Install Ubuntu (Latest)

After updating WSL2, install the latest Ubuntu version:

```powershell
wsl --install -d Ubuntu
```

Or to specify a particular version (e.g., Ubuntu 24.04 LTS):

```powershell
wsl --install -d Ubuntu-24.04
```

To see available Ubuntu versions:

```powershell
wsl --list -o
```

Set up your Ubuntu username and password when prompted.

After installation, close and reopen Windows Terminal. Ubuntu should now appear as a profile option.

## 4. Update and Upgrade Ubuntu Packages

Once in your Ubuntu terminal, update and upgrade all packages:

```bash
# Update package list
sudo apt update

# See available upgrades
apt list --upgradable

# Upgrade all packages
sudo apt upgrade -y

# Optional: autoremove old packages
sudo apt autoremove -y
```

## 5. Install Docker Engine in WSL2 Ubuntu

Open your WSL2 Ubuntu terminal and install Docker Engine directly:

```bash
# Install Docker Engine
sudo apt install -y docker.io

# Add your user to the docker group (to run without sudo)
sudo usermod -aG docker $USER

# Apply group changes
newgrp docker

# Enable Docker to start on boot (optional)
sudo systemctl enable docker
sudo systemctl start docker

# Verify installation
docker --version
docker run hello-world
```

## 6. (Optional) Make Docker Accessible from Windows PowerShell

If you want to run docker commands from PowerShell instead of just WSL, create an alias in your PowerShell profile.

### Find Your PowerShell Profile Location

In PowerShell, run:

```powershell
$PROFILE
```

This will display the path to your profile (usually something like `C:\Users\YourUsername\Documents\PowerShell\profile.ps1`).

### Create or Edit Your Profile

If the profile doesn't exist, create it first:

```powershell
New-Item -Path $PROFILE -Type File -Force
```

Then open it in your default editor:

```powershell
notepad $PROFILE
```

Or use VS Code:

```powershell
code $PROFILE
```

### Add the Docker Alias

In your profile file, add this line:

```powershell
function docker { wsl docker @args }
```

Save and close the file.

### Reload Your Profile

Close and reopen PowerShell, or reload the profile:

```powershell
. $PROFILE
```

Now you can run Docker commands directly from PowerShell:

```powershell
docker run hello-world
docker ps
```

## 7. (Optional) Install Podman Desktop as Docker Desktop Replacement

If you prefer a GUI application like Docker Desktop, install Podman Desktop for Windows:

### Install Podman Desktop

In PowerShell as Administrator:

```powershell
winget install RedHat.Podman-Desktop
```

Or use UniGetUI:

- Open UniGetUI
- Search for "Podman Desktop"
- Install Podman Desktop

> **Important:** Close and reopen PowerShell after installation so it recognizes the `podman` command.

### Launch Podman Desktop

After installation:

- Search for "Podman Desktop" in Windows Start menu
- Click to launch the application
- The app will automatically initialize and start the Podman machine

### View Images and Containers

In Podman Desktop, you'll see tabs for:

- **Images** - View all downloaded container images
- **Containers** - View running and stopped containers with their status, ports, and logs
- **Volumes** - Manage container storage volumes
- **Pods** - Manage Podman pods

You can manage containers directly from the GUI — start, stop, remove, view logs, etc.

### Command Line Access

You can still use Podman commands from PowerShell:

```powershell
# List images
podman image ls

# List running containers
podman ps

# Run a container
podman run hello-world

# View logs
podman logs <container-id>
```

---

## Appendix: Common Docker Commands

### Image Commands

```bash
# List all images
docker image ls
docker images

# Search for an image on Docker Hub
docker search ubuntu

# Pull an image
docker pull ubuntu
docker pull nginx:latest

# Remove an image
docker rmi <image-id>
docker rmi ubuntu

# View image details
docker inspect <image-id>
```

### Container Commands

```bash
# List running containers
docker ps

# List all containers (including stopped)
docker ps -a

# Run a container
docker run hello-world
docker run -it ubuntu /bin/bash
docker run -d nginx  # Run in background (detached)

# Run container with port mapping
docker run -p 8080:80 nginx  # Map port 80 inside container to 8080 on host

# Stop a running container
docker stop <container-id>

# Start a stopped container
docker start <container-id>

# Remove a container
docker rm <container-id>

# View container logs
docker logs <container-id>
docker logs -f <container-id>  # Follow logs in real-time

# Execute command in running container
docker exec -it <container-id> /bin/bash

# View container details
docker inspect <container-id>
```

### Container Management

```bash
# View resource usage (CPU, memory, etc.)
docker stats

# Rename a container
docker rename <old-name> <new-name>

# Copy files from container to host
docker cp <container-id>:/path/file.txt ./local-path/

# View running processes in container
docker top <container-id>
```

### Volume Commands

```bash
# List volumes
docker volume ls

# Create a volume
docker volume create my-volume

# Remove a volume
docker volume rm my-volume

# Run container with volume
docker run -v my-volume:/data nginx
```

### Cleanup Commands

```bash
# Remove stopped containers
docker container prune

# Remove dangling images
docker image prune

# Remove unused volumes
docker volume prune

# Remove everything unused (containers, images, volumes, networks)
docker system prune

# Remove everything forcefully
docker system prune -a
```

### Network Commands

```bash
# List networks
docker network ls

# Create a network
docker network create my-network

# Connect container to network
docker network connect my-network <container-id>

# Inspect network
docker inspect <network-id>
```

### Build Commands

```bash
# Build image from Dockerfile
docker build -t my-image:latest .

# Build with tag
docker build -t myname/my-image:1.0 .

# View build history
docker history <image-id>
```

### System Information

```bash
# Check version
docker --version

# View system info
docker info

# View events (real-time updates)
docker events
```

### Tips

- Use `-it` flags together: `docker run -it ubuntu` for interactive terminal
- Use `-d` flag to run containers in the background (detached mode)
- Use `-p` to map ports between host and container
- Use `-e` to set environment variables: `docker run -e VAR=value ubuntu`
- Use `--name` to give containers meaningful names: `docker run --name web-server nginx`
- Most commands accept either full container ID or the first few characters of it
- Tab completion works in PowerShell for docker commands (after setting up the alias)
#!/bin/bash

# build.sh - Build and test the project

set -e

echo "=== Journal Management - Build Script ==="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Step 1: Restore dependencies
echo -e "${YELLOW}[1/5] Restoring dependencies...${NC}"
dotnet restore

# Step 2: Build
echo -e "${YELLOW}[2/5] Building project...${NC}"
dotnet build --configuration Release

# Step 3: Run tests
echo -e "${YELLOW}[3/5] Running tests...${NC}"
dotnet test --configuration Release --no-build

# Step 4: Docker build
echo -e "${YELLOW}[4/5] Building Docker images...${NC}"
docker-compose build --no-cache

# Step 5: Summary
echo -e "${YELLOW}[5/5] Build complete!${NC}"
echo ""
echo -e "${GREEN}✓ Build successful!${NC}"
echo ""
echo "Next steps:"
echo "  docker compose up -d       # Start services"
echo "  docker compose ps          # Check status"
echo "  http://localhost/swagger   # View API docs"
echo ""

# ------------------ BUILD STAGE ------------------
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src

# 1) ������� csproj-���� � ���������� ���������
COPY ["SteamStoreBot.csproj", "./"]
RUN dotnet restore "./SteamStoreBot.csproj"

# 2) ������� ����� ����� ������
COPY . .

# 3) �������� � Release-������������
RUN dotnet publish "SteamStoreBot.csproj" -c Release -o /app/publish

# ------------------ RUNTIME STAGE ------------------
FROM mcr.microsoft.com/dotnet/runtime:7.0 AS runtime
WORKDIR /app

# 4) ������� ����� ��������� � ������������ �����
COPY --from=build /app/publish ./ 

# 5) ������� ��� ���� ������������ � botConfig.json, ������������, �� ���� �������� � ���������� �����.
# ���� �� ������ ��������� ���� ����� ���� ��������, �� ���� ����� ��������, 
# ��� ���� �� ������ ���� ������ (��� ������), ����� ������.

# 6) ������� ������� �������
ENTRYPOINT ["dotnet", "SteamStoreBot.dll"]

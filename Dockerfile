########################################
# 1) BUILD STAGE
########################################

# ������� ����� � ������ SDK .NET 8.0 (Linux)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build 
WORKDIR /src

# 1.1) ������� ����� .csproj, ��� ������������ ����� Docker ��� dotnet restore
#      ���� � ��� ���� ����� (�� "SteamStoreBot_V8.csproj"), ������ �� ��������.
COPY ["SteamStoreBot_V8.csproj", "./"]

# 1.2) �������� dotnet restore, ���� ����������� �� PackageReference
RUN dotnet restore "SteamStoreBot_V8.csproj"

# 1.3) ������� ������� ���� �������� ��� � ���������
COPY . .

# 1.4) �������� dotnet publish (Release), ���������� ���������� � ����� /app/publish
RUN dotnet publish "SteamStoreBot_V8.csproj" -c Release -o /app/publish


########################################
# 2) RUNTIME STAGE
########################################

# ������� ����� ����� � .NET Runtime 8.0 (Linux) � ��� ��� SDK
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# 2.1) ������� ��������� ��������� � ������������ �����
COPY --from=build /app/publish ./

# 2.2) ������������ ����� �����:
#      ��� �� ��������� ���� ������������ .dll ����.
ENTRYPOINT ["dotnet", "SteamStoreBot_V8.dll"]

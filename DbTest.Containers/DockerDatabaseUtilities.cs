using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace DbTest.Containers
{
    public class DockerDatabaseUtilities
    {
        public const string FakePort = "80";
        public string Host;
        public string PortInContainer;
        public string DbPassword;
        public string DbUser;
        public string DbName;
        public string DbImage;
        public List<string> Env;

        private readonly string _dbContainerName = $"IntegrationTestingContainer_{Guid.NewGuid()}";
        private readonly string _dbVolumeName = $"IntegrationTestingVolume_{Guid.NewGuid()}";

        public async Task<(string containerId, string port)> EnsureDockerStartedAndGetContainerIdAndPortAsync(DbConnection connection)
        {
            await CleanupRunningContainers();
            await CleanupRunningVolumes();
            var dockerClient = GetDockerClient();
            var freePort = GetFreePort();

            await EnsureDockerImagePulled(dockerClient);

            await CreateVolumeIfNonExists(dockerClient);

            // create container, if one doesn't already exist
            var contList = await dockerClient
                .Containers.ListContainersAsync(new ContainersListParameters { All = true });
            var existingCont = contList.FirstOrDefault(c => c.Names.Any<string>(n => n.Contains(_dbContainerName)));

            if (existingCont != null)
            {
                return (existingCont.ID, existingCont.Ports.FirstOrDefault()?.PublicPort.ToString())!;
            }

            var sqlContainer = await dockerClient
                .Containers
                .CreateContainerAsync(new CreateContainerParameters
                {
                    Name = _dbContainerName,
                    Image = DbImage,
                    Env = Env,
                    HostConfig = new HostConfig
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>>
                        {
                            { PortInContainer, new[] { new PortBinding { HostPort = freePort } } }
                        }
                    }
                });

            await dockerClient
                .Containers
                .StartContainerAsync(sqlContainer.ID, new ContainerStartParameters());

            connection.ConnectionString = connection.ConnectionString.Replace(FakePort, freePort);

            await WaitUntilDatabaseAvailableAsync(connection);
            return (sqlContainer.ID, freePort);
        }

        private async Task CreateVolumeIfNonExists(DockerClient dockerClient)
        {
            var volumeList = await dockerClient.Volumes.ListAsync();
            var volumeCount = volumeList.Volumes.Count(v => v.Name == _dbVolumeName);
            if (volumeCount <= 0)
            {
                await dockerClient.Volumes.CreateAsync(new VolumesCreateParameters { Name = _dbVolumeName, });
            }
        }

        private async Task EnsureDockerImagePulled(DockerClient dockerClient) =>
            await dockerClient.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = DbImage }, null,
                new Progress<JSONMessage>());


        private static bool IsRunningOnWindows() => Environment.OSVersion.Platform == PlatformID.Win32NT;

        private DockerClient GetDockerClient()
        {
            var dockerUri = IsRunningOnWindows()
                ? "npipe://./pipe/docker_engine"
                : "unix:///var/run/docker.sock";
            return new DockerClientConfiguration(new Uri(dockerUri))
                .CreateClient();
        }

        private async Task CleanupRunningContainers(int hoursTillExpiration = -24)
        {
            var dockerClient = GetDockerClient();

            var runningContainers = await dockerClient.Containers
                .ListContainersAsync(new ContainersListParameters());

            foreach (var runningContainer in runningContainers.Where(cont =>
                         cont.Names.Any(n => n.Contains(_dbContainerName))))
            {
                // Stopping all test containers that are older than 24 hours
                var expiration = hoursTillExpiration > 0
                    ? hoursTillExpiration * -1
                    : hoursTillExpiration;
                if (runningContainer.Created >= DateTime.UtcNow.AddHours(expiration))
                {
                    continue;
                }

                try
                {
                    await EnsureDockerContainersStoppedAndRemovedAsync(runningContainer.ID);
                }
                catch
                {
                    // Ignoring failures to stop running containers
                }
            }
        }

        private async Task CleanupRunningVolumes(int hoursTillExpiration = -24)
        {
            var dockerClient = GetDockerClient();

            var runningVolumes = await dockerClient.Volumes.ListAsync();

            foreach (var runningVolume in runningVolumes.Volumes.Where(v => v.Name == _dbVolumeName))
            {
                // Stopping all test volumes that are older than 24 hours
                var expiration = hoursTillExpiration > 0
                    ? hoursTillExpiration * -1
                    : hoursTillExpiration;
                if (DateTime.Parse(runningVolume.CreatedAt) >= DateTime.UtcNow.AddHours(expiration))
                {
                    continue;
                }

                try
                {
                    await EnsureDockerVolumesRemovedAsync(runningVolume.Name);
                }
                catch
                {
                    // Ignoring failures to stop running containers (here it didn't help)
                }
            }
        }

        public async Task EnsureDockerContainersStoppedAndRemovedAsync(string dockerContainerId)
        {
            try
            {
                var dockerClient = GetDockerClient();
                await dockerClient.Containers
                    .StopContainerAsync(dockerContainerId, new ContainerStopParameters());
                await dockerClient.Containers
                    .RemoveContainerAsync(dockerContainerId, new ContainerRemoveParameters());
            }
            catch (Exception)
            {
                //todo temp and dirty solution
            }
        }

        private async Task EnsureDockerVolumesRemovedAsync(string volumeName)
        {
            try
            {
                var dockerClient = GetDockerClient();
                await dockerClient.Volumes.RemoveAsync(volumeName);
            }
            catch (Exception)
            {
                //todo temp and dirty solution
            }
        }

        private async Task WaitUntilDatabaseAvailableAsync(DbConnection connection)
        {
            var start = DateTime.UtcNow;
            const int maxWaitTimeSeconds = 60;
            var connectionEstablished = false;
            while (!connectionEstablished && start.AddSeconds(maxWaitTimeSeconds) > DateTime.UtcNow)
            {
                try
                {
                    await connection.OpenAsync();
                    connectionEstablished = true;
                }
                catch
                {
                    // If opening the connection fails, database is not ready yet
                    await Task.Delay(500);
                }
            }

            if (!connectionEstablished)
            {
                throw new Exception(
                    $"Connection to the SQL docker database could not be established within {maxWaitTimeSeconds} seconds.");
            }

            await connection.CloseAsync();
            await connection.DisposeAsync();
        }

        private static string GetFreePort()
        {
            // From https://stackoverflow.com/a/150974/4190785
            var tcpListener = new TcpListener(IPAddress.Loopback, 0);
            tcpListener.Start();
            var port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();
            return port.ToString();
        }
    }
}

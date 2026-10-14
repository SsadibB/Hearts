using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using Hearts.Persistence;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hearts.Networking
{
    public class HeartsSession : MonoBehaviour, INetworkRunnerCallbacks
    {
        [SerializeField] NetworkRunner runner;
        [SerializeField] NetworkSceneManagerDefault sceneManager;
        [SerializeField] NetworkHeartsMatch match;
        [SerializeField] NetworkHeartsPlayer[] seats;

        bool _starting;

        public bool IsRunning => runner != null && runner.IsRunning;
        public string LastMessage { get; private set; } = "Choose a game.";

        void Awake()
        {
            if (runner != null)
                runner.AddCallbacks(this);
        }

        void OnDestroy()
        {
            if (runner != null)
                runner.RemoveCallbacks(this);
        }

        public async void StartHost(string roomName)
        {
            await StartMode(GameMode.Host, roomName);
        }

        public async void StartClient(string roomName)
        {
            await StartMode(GameMode.Client, roomName);
        }

        public async void Leave()
        {
            if (runner != null && runner.IsRunning)
                await runner.Shutdown();
            LastMessage = "Left the room.";
        }

        async System.Threading.Tasks.Task StartMode(GameMode mode, string roomName)
        {
            if (_starting || runner == null)
                return;
            if (runner.IsRunning)
            {
                LastMessage = "Already connected.";
                return;
            }

            _starting = true;
            roomName = string.IsNullOrWhiteSpace(roomName) ? "Hearts" : roomName.Trim();
            LastMessage = mode == GameMode.Host ? "Hosting " + roomName + "..." : "Joining " + roomName + "...";
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = roomName,
                PlayerCount = 4,
                Scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager = sceneManager
            });
            _starting = false;
            LastMessage = result.Ok ? "Connected to " + roomName + "." : result.ShutdownReason.ToString();
            if (result.Ok)
                SendLocalProfile();
        }

        public void SendLocalProfile()
        {
            if (runner == null || !runner.IsRunning)
                return;
            var settings = SaveManager.Load();
            var seat = LocalSeat();
            if (seat != null)
                seat.SetLocalProfile(settings.DisplayName, settings.AvatarIndex);
        }

        public NetworkHeartsPlayer LocalSeat()
        {
            if (seats == null)
                return null;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null && seats[i].Object != null && seats[i].Object.IsValid && seats[i].HasInputAuthority)
                    return seats[i];
            }
            return null;
        }

        public void OnPlayerJoined(NetworkRunner networkRunner, PlayerRef player)
        {
            if (!networkRunner.IsServer || match == null)
                return;
            NetworkHeartsPlayer seat = null;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null && seats[i].Occupied && !seats[i].Connected)
                {
                    seat = seats[i];
                    break;
                }
            }
            if (seat == null)
            {
                for (int i = 0; i < seats.Length; i++)
                {
                    if (seats[i] != null && !seats[i].Occupied)
                    {
                        seat = seats[i];
                        break;
                    }
                }
            }
            if (seat == null)
                return;
            seat.Object.AssignInputAuthority(player);
            match.ServerSeatJoined(seat);
            if (player == networkRunner.LocalPlayer)
                SendLocalProfile();
        }

        public void OnPlayerLeft(NetworkRunner networkRunner, PlayerRef player)
        {
            if (!networkRunner.IsServer)
                return;
            for (int i = 0; i < seats.Length; i++)
            {
                if (seats[i] != null && seats[i].Object != null && seats[i].Object.InputAuthority == player)
                {
                    match.ServerSeatLeft(seats[i].Seat);
                    return;
                }
            }
        }

        public void OnShutdown(NetworkRunner networkRunner, ShutdownReason shutdownReason)
        {
            LastMessage = shutdownReason.ToString();
        }

        public void OnConnectedToServer(NetworkRunner networkRunner)
        {
            LastMessage = "Connected.";
            SendLocalProfile();
        }

        public void OnDisconnectedFromServer(NetworkRunner networkRunner, NetDisconnectReason reason)
        {
            LastMessage = reason.ToString();
        }

        public void OnConnectRequest(NetworkRunner networkRunner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (networkRunner.SessionInfo.PlayerCount >= 4)
                request.Refuse();
            else
                request.Accept();
        }

        public void OnConnectFailed(NetworkRunner networkRunner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            LastMessage = reason.ToString();
        }

        public void OnInput(NetworkRunner networkRunner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner networkRunner, PlayerRef player, NetworkInput input) { }
#pragma warning disable CS0618
        public void OnUserSimulationMessage(NetworkRunner networkRunner, SimulationMessagePtr message) { }
#pragma warning restore CS0618
        public void OnSessionListUpdated(NetworkRunner networkRunner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner networkRunner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner networkRunner, HostMigrationToken hostMigrationToken) { }
        public void OnSceneLoadDone(NetworkRunner networkRunner) { }
        public void OnSceneLoadStart(NetworkRunner networkRunner) { }
        public void OnObjectExitAOI(NetworkRunner networkRunner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectEnterAOI(NetworkRunner networkRunner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, System.ReadOnlySpan<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner networkRunner, PlayerRef player, ReliableKey key, float progress) { }
    }
}

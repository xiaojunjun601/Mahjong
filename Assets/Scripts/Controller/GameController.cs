using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cinemachine;
using DG.Tweening;
using Manager;
using Newtonsoft.Json;
using Oculus.Avatar2;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using Photon.Pun;
using PlayFab;
using PlayFab.ClientModels;
using RenderHeads.Media.AVProMovieCapture;
using TMPro;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Random = System.Random;

namespace Controller
{
    /// <summary>
    /// 负责管理整个游戏的逻辑,单例
    /// </summary>
    public class GameController : MonoBehaviourPunCallbacks
    {
        public static GameController Instance { get; private set; }
        private int _playerCount;
        [HideInInspector] public PlayerController myPlayerController;
        public Dictionary<int, int> ReadyDict;
        [HideInInspector] public int nowTurn;
        [HideInInspector] public int nowTile;
        [HideInInspector] public int tileViewID;
        private List<MahjongAttr> _mahjong;
        [SerializeField] private Transform[] playerCapturers;
        [SerializeField] private Transform[] playerMenus;
        [SerializeField] private Transform[] questPanels;
        [SerializeField] private Transform[] playerCanvases;
        [SerializeField] private Transform[] playerCardContainers;
        [SerializeField] private Transform[] playerButtonContainers;
        [SerializeField] private Transform[] playerResultPanels;
        [SerializeField] private Transform[] playerPunishPanels;

        [Header("-------------麻将前面显示玩家积分的Text----------------"), SerializeField]
        private TMP_Text[] playerScoreTexts;

        private bool _canPong;
        private bool _canKong;
        private bool _canWin;
        // Start Edit
        private int _kongCount;
        // End Edit
        private Button _confirmButton;
        public Material[] transparentMaterials;
        public Material[] normalMaterials;
        [SerializeField] private GameObject WinnerHat;

        [SerializeField] private GameObject LoserNose;
        public GameObject effectPrefab;
        public GameObject bubbleEffect;
        public Transform[] playerPanelContainers;
        public GameObject playerPanelPrefab;
        public GazeInteractor GazeInteractor;
        private static Random rng = new();
        public GameObject nowMahjong;
        public AudioTrigger changeMahjongAudio;

        [SerializeField, Interface(typeof(IHand))]
        public MonoBehaviour _leftHand;

        [SerializeField, Interface(typeof(IHand))]
        public MonoBehaviour _rightHand;

        [FormerlySerializedAs("_transformer")]
        [SerializeField, Interface(typeof(ITrackingToWorldTransformer))]
        [Tooltip("Transformer is required so calculations can be done in Tracking space")]
        public UnityEngine.Object _transformer;

        public OVRCameraRig OvrCameraRig;
        public GameObject BunnyEar;

        /// <summary>
        /// 本局游戏看牌数
        /// </summary>
        public int totalCount;

        /// <summary>
        /// 本局游戏回合数
        /// </summary>
        public int totalRound;

        /// <summary>
        /// 用于录制的摄像机
        /// </summary>
        [SerializeField] private CinemachineVirtualCamera recordingCamera;

        [SerializeField] private CaptureFromTexture _captureFromTexture;

        [SerializeField] private RenderTexture _captureTexture;

        //private int _playerDictCount;
        [HideInInspector] public List<MahjongAttr> effectGoList = new();


        /// <summary>
        /// 初始化
        /// </summary>
        private void Awake()
        {
            if (Instance)
            {
                Destroy(gameObject);
            }

            //初始化单例
            Instance = this;
            //初始化麻将位置
            GameManager.Instance.InitWhenStart();
            //初始化总回合数，房主当前回合为1，其他为0
            totalRound = PhotonNetwork.IsMasterClient ? 1 : 0;
            //视线数据收集，房主开局就开始收集，其他玩家开局不收集
            GazeInteractor.startCount = PhotonNetwork.IsMasterClient;
            //总查看手牌次数开局为0
            totalCount = 0;
            //获取玩家个数
            _playerCount = PhotonNetwork.CurrentRoom.PlayerCount;
            //初始化每回合的字典
            ReadyDict = new Dictionary<int, int>();
            //初始化所有麻将的字典
            _mahjong = new List<MahjongAttr>();
            //playerButtons = new List<Transform>();
            //开始游戏
            StartGame();
            //初始化Avatar的委托，减少手部穿透
            FindObjectOfType<OvrAvatarManager>().GetComponent<AvatarInputManager>().Init();
        }

        /// <summary>
        /// 退出房间
        /// </summary>
        public override void OnLeftRoom()
        {
            PhotonNetwork.LoadLevel(1);
        }

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            GeneratePlayers();
            GetPlayerScore();
            _kongCount = 0;
            //房主检测是否天胡
            var isWin = PhotonNetwork.IsMasterClient && CheckWin();
            var canK = false;
            //所有玩家检测是否能杠
            foreach (var pair in myPlayerController.MyMahjong)
            {
                if (pair.Value.Count == 4)
                {
                    canK = true;
                    if (isWin)
                    {
                        photonView.RPC(nameof(CanKAndH), RpcTarget.All, myPlayerController.playerID);
                        break;
                    }

                    photonView.RPC(nameof(CanK), RpcTarget.All, myPlayerController.playerID);
                    break;
                }
            }

            if (isWin && !canK)
            {
                photonView.RPC(nameof(CanH), RpcTarget.All, myPlayerController.playerID);
            }
        }

        [PunRPC]
        private void SetList(string a, string b)
        {
            GameManager.Instance.SetMahjongList(JsonConvert.DeserializeObject<List<Mahjong>>(a));
            GameManager.Instance.SetUserMahjongLists(
                JsonConvert.DeserializeObject<List<List<Mahjong>>>(b));
            var count = 14 + (PhotonNetwork.CurrentRoom.PlayerCount - 1) * 13;
            _mahjong = FindObjectsOfType<MahjongAttr>().ToList();
            _mahjong.Sort((nameA, nameB) =>
                int.Parse(nameA.gameObject.name.Split('_')[2])
                    .CompareTo(int.Parse(nameB.gameObject.name.Split('_')[2])));
            for (var i = 0; i < count; i++)
            {
                Destroy(_mahjong[i].gameObject);
            }

            _mahjong.RemoveRange(0, count);
            var mahjongList = GameManager.Instance.GetMahjongList();
            var length = _mahjong.Count;
            for (var i = 0; i < length; i++)
            {
                _mahjong[i].GetComponent<MeshFilter>().mesh =
                    GameManager.Instance.GetMahjongMesh(mahjongList[i].ID);
                var rb = _mahjong[i].GetComponent<Rigidbody>();
                var attr = _mahjong[i].GetComponent<MahjongAttr>();
                attr.ID = mahjongList[i].ID;
                rb.constraints = RigidbodyConstraints.FreezeAll;
                _mahjong[i].GetComponent<HandGrabInteractable>().enabled = false;
                //_mahjong[i].GetComponent<TouchHandGrabInteractable>().enabled = false;
            }

            //生成当前玩家的麻将有序字典
            myPlayerController.MyMahjong =
                GameManager.Instance.GenerateMahjongAtStart(myPlayerController.playerID - 1);
            SortMyMahjong(true, false);
            // for (var i = 1; i <= 4; i++)
            // {
            //     playerButtons.Add(GameObject.Find("Player" + i + "Button").transform);
            // }
            var pongButton = playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).GetChild(0);
            pongButton.GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(SolvePong);
            var kongButton = playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).GetChild(0);
            kongButton.GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(SolveKong);
            var winButton = playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).GetChild(0);
            winButton.GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(SolveWin);
            var skipButton = playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).GetChild(0);
            skipButton.GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(SolveSkip);
            var leaveButton = playerResultPanels[myPlayerController.playerID - 1].GetChild(2).GetChild(0);
            leaveButton.GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(LeaveGame);
            var punishButton = playerResultPanels[myPlayerController.playerID - 1].GetChild(3).GetChild(0);
            punishButton.GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(OpenPlayerPunishPanel);
            _captureFromTexture.SetSourceTexture(_captureTexture);
            playerPunishPanels[myPlayerController.playerID - 1].GetChild(3).GetChild(0)
                .GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(() =>
                {
                    _captureFromTexture.StartCapture();
                });
            playerPunishPanels[myPlayerController.playerID - 1].GetChild(4).GetChild(0)
                .GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(() =>
                {
                    _captureFromTexture.StopCapture();
                    photonView.RPC(nameof(OpenPlayerResultPanel), RpcTarget.All);
                });
            // foreach (var playerButton in playerButtonContainers)
            // {
            //     for (var i = 0; i < 5; i++)
            //     {
            //         playerButton.GetChild(i).gameObject.SetActive(false);
            //     }
            //
            //     playerButton.GetChild(6).gameObject.SetActive(false);
            // }
            nowTurn = 1;
        }


        private static void LeaveGame()
        {
            PhotonNetwork.LeaveRoom();
        }

        private void GetPlayerScore()
        {
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
                data =>
                {
                    photonView.RPC(nameof(UpdatePoint), RpcTarget.All, myPlayerController.playerID,
                        int.Parse(data.Data["Score"].Value));
                },
                error => { Debug.Log(error.ErrorMessage); });
        }

        [PunRPC]
        private void UpdatePoint(int id, int point)
        {
            playerScoreTexts[id - 1].text = $"分数：{point.ToString()}";
        }

        [PunRPC]
        private void SetPoint(int id, string userName, int kongCount)
        {
            foreach (var mahjong in FindObjectsOfType<MahjongAttr>())
            {
                Destroy(mahjong.gameObject);
            }

            if (PhotonNetwork.LocalPlayer.NickName == userName)
            {
                var go = PhotonNetwork.Instantiate("m_CrownHat02", Vector3.zero, quaternion.identity);
                go.transform.SetParent(myPlayerController.transform.GetChild(0));
                go.transform.SetLocalPositionAndRotation(new Vector3(0.15f, 0.02f, 0f), Quaternion.Euler(0f, 0f, -90f));
            }
            else
            {
                var go = PhotonNetwork.Instantiate("NasoClown", Vector3.zero, quaternion.identity);
                go.transform.SetParent(myPlayerController.transform.GetChild(0));
                go.transform.SetLocalPositionAndRotation(new Vector3(0.04f, 0.16f, 0f), Quaternion.Euler(-90f, 0f, 0f));
                var bunnyEar = PhotonNetwork.Instantiate("m_Bunnyears01", Vector3.zero, quaternion.identity);
                bunnyEar.transform.SetParent(myPlayerController.transform.GetChild(0));
                bunnyEar.transform.SetLocalPositionAndRotation(new Vector3(0.14f, 0.04f, 0f),
                    Quaternion.Euler(-90f, 0f, -90f));
                bunnyEar.transform.localScale = new Vector3(0.045f, 0.045f, 0.045f);
            }


            OpenPlayerResultPanel();
            playerResultPanels[myPlayerController.playerID - 1].GetChild(3).gameObject
                .SetActive(id == myPlayerController.playerID);
            playerResultPanels[myPlayerController.playerID - 1].GetChild(2).gameObject
                .SetActive(false);
            // var scoreCanvas = playerCanvases[myPlayerController.playerID - 1].GetChild(4).gameObject;
            // scoreCanvas.SetActive(true);
            // scoreCanvas.transform.GetChild(1).GetComponentInChildren<TMP_Text>().text =
            //     id == myPlayerController.playerID ? "您赢了！" : "您输了！";
            playerResultPanels[myPlayerController.playerID - 1].GetChild(0).GetChild(0).GetComponent<TMP_Text>()
                .text = id == myPlayerController.playerID
                ? $"您赢了！\n本局游戏您平均每次出牌前查看{totalCount / totalRound}次牌"
                : $"您输了！\n本局游戏您平均每次出牌前查看{totalCount / totalRound}次牌";
            var i = 0;
            foreach (var playerPair in PhotonNetwork.CurrentRoom.Players)
            {
                playerResultPanels[myPlayerController.playerID - 1].GetChild(1).GetChild(i).gameObject
                    .SetActive(true);
                playerResultPanels[myPlayerController.playerID - 1].GetChild(1).GetChild(i).GetChild(0)
                    .GetComponent<TMP_Text>().text = playerPair.Value.NickName;
                playerResultPanels[myPlayerController.playerID - 1].GetChild(1).GetChild(i).GetChild(1)
                    .GetComponent<TMP_Text>().text = playerPair.Value.NickName == userName
                //Start Edit
                    ? "积分 + " + (50 * (kongCount == 0 ? 1 : kongCount + 1) * (PhotonNetwork.CurrentRoom.PlayerCount - 1)) 
                    : "积分 - " + (50 * (kongCount == 0 ? 1 : kongCount + 1));
                // End Edit
                i++;
            }

            for (; i < 4; i++)
            {
                playerResultPanels[myPlayerController.playerID - 1].GetChild(1).GetChild(i).gameObject
                    .SetActive(false);
            }


            // foreach (var item in PhotonNetwork.CurrentRoom.Players)
            // {
            //     var go = Instantiate(playerPanelPrefab, playerPanelContainers[myPlayerController.playerID - 1]);
            //     go.transform.GetChild(0).GetComponent<TMP_Text>().text = item.Value.NickName;
            //     go.transform.GetChild(1).GetComponent<TMP_Text>().text =
            //         item.Value.NickName == userName ? "Score + 50" : "Score - 50";
            // }

            int score;
            PlayFabClientAPI.GetUserData(new GetUserDataRequest { }, data =>
                {  
                    
                    score = int.Parse(data.Data["Score"].Value);
                    score = id == myPlayerController.playerID 
                        // Start Edit
                        ? score + (50 * (kongCount == 0 ? 1 : kongCount + 1) * (PhotonNetwork.CurrentRoom.PlayerCount - 1))   
                        : score - (50 * (kongCount == 0 ? 1 : kongCount + 1));
                    Debug.Log($"Now score:{score}");
                    // End Edit
                    PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                        {
                            Data = new Dictionary<string, string>
                            {
                                { "Score", score.ToString() },
                            }
                        },
                        _ => { GetPlayerScore(); },
                        _ => { });
                },
                error => { Debug.Log(error.ErrorMessage); });
        }

        [PunRPC]
        private void OpenPlayerResultPanel()
        {
            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
            questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].GetChild(3).gameObject
                .SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].GetChild(2).gameObject
                .SetActive(true);
        }

        private void OpenPlayerButtonContainer()
        {
            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
            questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
        }

        private void ClosePlayerButtonContainer()
        {
            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
            questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
        }

        private void OpenPlayerPunishPanel()
        {
            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
            questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(true);
            var playerPunishPanelContainer = playerPunishPanels[myPlayerController.playerID - 1].GetChild(1);
            var i = 0;
            foreach (var playerPair in PhotonNetwork.CurrentRoom.Players)
            {
                if (playerPair.Value.NickName == GameManager.Instance.GetPlayerName())
                {
                    playerPunishPanelContainer.GetChild(i).gameObject.SetActive(false);
                    i++;
                    continue;
                }

                playerPunishPanelContainer.GetChild(i).gameObject.SetActive(true);
                playerPunishPanelContainer.GetChild(i).GetChild(0).GetComponent<TMP_Text>().text =
                    playerPair.Value.NickName;
                playerPunishPanelContainer.GetChild(i).GetChild(1).GetChild(0)
                    .GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(
                        () =>
                        {
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
                            var punishedPlayer = GameObject.Find(playerPair.Value.NickName);
                            var playerID = punishedPlayer.GetComponent<PlayerController>().playerID;
                            photonView.RPC(nameof(OpenOrCloseUI), RpcTarget.Others, playerID,
                                playerPair.Value.NickName);
                            recordingCamera.LookAt = punishedPlayer.transform.GetChild(0);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(4).gameObject.SetActive(true);
                        });
                playerPunishPanelContainer.GetChild(i).GetChild(2).GetChild(0)
                    .GetComponent<InteractableUnityEventWrapper>().WhenUnselect.AddListener(
                        () =>
                        {
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
                            var punishedPlayer = GameObject.Find(playerPair.Value.NickName);
                            var playerID = punishedPlayer.GetComponent<PlayerController>().playerID;
                            photonView.RPC(nameof(OpenOrCloseUI), RpcTarget.Others, playerID,
                                playerPair.Value.NickName);
                            recordingCamera.LookAt = punishedPlayer.transform.GetChild(0);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
                            playerPunishPanels[myPlayerController.playerID - 1].GetChild(4).gameObject.SetActive(true);
                        });
                i++;
            }

            for (; i < 4; i++)
            {
                playerPunishPanelContainer.GetChild(i).gameObject.SetActive(false);
            }
        }

        [PunRPC]
        private void OpenOrCloseUI(int id, string playerName)
        {
            if (myPlayerController.playerID == id)
            {
                playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            }
            else
            {
                playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
                playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(true);
                playerPunishPanels[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
                playerPunishPanels[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
                var punishedPlayer = GameObject.Find(playerName);
                recordingCamera.LookAt = punishedPlayer.transform.GetChild(0);
                playerPunishPanels[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
                playerPunishPanels[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
                playerPunishPanels[myPlayerController.playerID - 1].GetChild(4).gameObject.SetActive(true);
            }
        }

        public void OpenPlayerCardContainer()
        {
            //通过PlayFab获取道具数量，显示在UI上
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
                //在CallBack函数中打开UI，防止提前打开UI
                data =>
                {
                    //找到两张牌的数量
                    var xRayCardCount = int.Parse(data.Data["XRayCard"].Value);
                    var cheatCardCount = int.Parse(data.Data["CheatCard"].Value);
                    //打开所有的UI
                    playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
                    playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
                    questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                    playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                    playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                    playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(true);
                    playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                    //更新数量
                    var xRayCardGo = playerCardContainers[myPlayerController.playerID - 1].GetChild(0);
                    xRayCardGo.GetComponentInChildren<TMP_Text>().text = $"透视道具\n当前拥有：{xRayCardCount}个";
                    var cheatCardGo = playerCardContainers[myPlayerController.playerID - 1].GetChild(1);
                    cheatCardGo.GetComponentInChildren<TMP_Text>().text = $"换牌道具\n当前拥有：{cheatCardCount}个";
                },
                error => { Debug.Log(error.ErrorMessage); });
        }

        private void ClosePlayerCardContainer()
        {
            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
        }

        private void SolveWin()
        {
            photonView.RPC(nameof(SetPoint), RpcTarget.All, myPlayerController.playerID,
                PhotonNetwork.LocalPlayer.NickName, _kongCount);
        }

        /// <summary>
        /// 因为可能有多个玩家同时可以处理牌，当一个玩家点击跳过，可操作玩家数--，当可操作玩家数等于0，才发牌
        /// </summary>
        private void SolveSkip()
        {
            if (!_canKong && !_canPong && !_canWin) return;
            //photonView.RPC(nameof(DecreasePlayerDictCount), RpcTarget.MasterClient);
            _canKong = _canPong = _canWin = false;
            //点击跳过，只是我跳过
            ResetButton();
            EnableHandGrab();
        }

        // [PunRPC]
        // private void DecreasePlayerDictCount()
        // {
        // 	_playerDictCount--;
        // 	if (_playerDictCount == 0)
        // 	{
        // 		photonView.RPC(nameof(NextTurn), RpcTarget.All, nowTurn, false);
        // 	}
        // }

        [PunRPC]
        private void ResetButton()
        {
            //隐藏碰
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(true);
            //隐藏杠
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
            //隐藏胡
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
            //隐藏跳过
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            ClosePlayerButtonContainer();
        }

        private void AddMahjongToHand(MahjongAttr attr)
        {
            totalRound++;
            GazeInteractor.startCount = true;
            attr.inMyHand = true;
            attr.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.None;
            attr.num = 1;
            attr.inOthersHand = false;
            attr.photonView.RPC(nameof(attr.RPCSetInMyHand), RpcTarget.Others, false);
            attr.photonView.RPC(nameof(attr.RPCSetInOthersHand), RpcTarget.Others, true);
            attr.photonView.RPC(nameof(attr.RPCSetOnDesk), RpcTarget.All, false);
            attr.photonView.RPC(nameof(attr.RPCSetIsThrown), RpcTarget.All, false);
            attr.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            attr.photonView.RPC(nameof(attr.RPCSetLayer), RpcTarget.Others, LayerMask.NameToLayer("Mahjong"));

            //新牌，把所有监听事件移除，然后添加监听事件
            attr.pointableUnityEventWrapper.WhenUnselect.RemoveAllListeners();
            attr.pointableUnityEventWrapper.WhenSelect.RemoveAllListeners();
            attr.pointableUnityEventWrapper.WhenUnselect.AddListener(attr.OnPut);
            attr.pointableUnityEventWrapper.WhenSelect.AddListener(attr.OnGrab);
            attr.GetComponent<HandGrabInteractable>().enabled = true;
            photonView.RPC(nameof(RemoveMahjong), RpcTarget.All);

            if (!myPlayerController.MyMahjong.ContainsKey(attr.ID))
            {
                myPlayerController.MyMahjong[attr.ID] = new List<GameObject>();
            }

            var canKong = false;
            if (myPlayerController.MyMahjong[attr.ID].Count == 3)
            {
                DisableHandGrab();
                //可以杠
                canKong = true;
            }

            //自摸
            if (CheckWin(attr.ID))
            {
                photonView.RPC(canKong ? nameof(CanKAndH) : nameof(CanH), RpcTarget.All, myPlayerController.playerID);
            }

            else if (canKong)
            {
                photonView.RPC(nameof(CanK), RpcTarget.All, myPlayerController.playerID);
            }

            myPlayerController.MyMahjong[attr.ID].Add(attr.gameObject);
        }

        [PunRPC]
        private void RemoveMahjong()
        {
            _mahjong.RemoveAt(0);
        }

        [PunRPC]
        public void NextTurn(int id, bool needDrawTile)
        {
            nowTurn = id;
            // My Turn
            if (myPlayerController.playerID == id)
            {
                // needDrawTile
                if (needDrawTile)
                {
                    _mahjong[0].GetComponent<PhotonView>()
                        .TransferOwnership(PhotonNetwork.LocalPlayer);

                    DOTween.Sequence().Insert(0f, _mahjong[0].transform.DOMove(myPlayerController.putPos, 1f))
                        .Insert(0f,
                            _mahjong[0].transform.DORotate(GameManager.Instance.GetRotateList()[id - 1], 1f))
                        .SetEase(Ease.Linear)
                        .onComplete += SolveMahjong;
                }
            }
        }

        private void SolveMahjong()
        {
            var id = _mahjong[0].ID;
            _mahjong[0].GetComponent<Rigidbody>().Sleep();
            AddMahjongToHand(_mahjong[0]);
            var idx = 1;
            foreach (var item in myPlayerController.MyMahjong)
            {
                foreach (var iGameObject in item.Value)
                {
                    var script = iGameObject.GetComponent<MahjongAttr>();
                    if (script.num == 0)
                    {
                        continue;
                    }

                    script.num = idx++;
                }
            }

            myPlayerController.mahjongMap[myPlayerController.MyMahjong[id].Count - 1].Remove(id);
            myPlayerController.mahjongMap[myPlayerController.MyMahjong[id].Count].Add(id);
        }

        /// <summary>
        /// 生成n个玩家
        /// </summary>
        private void GeneratePlayers()
        {
            var players = PhotonNetwork.CurrentRoom.Players;
            var index = 1 +
                        players.Count(player => player.Key < PhotonNetwork.LocalPlayer.ActorNumber);
            foreach (var player in players)
            {
                if (player.Value.IsLocal)
                {
                    var playerController = GameManager.Instance.GeneratePlayer(index - 1)
                        .GetComponent<PlayerController>();
                    myPlayerController = playerController;
                    myPlayerController.playerID = index;
                    myPlayerController.putPos = GameManager.Instance.GetNewPositions()[myPlayerController.playerID - 1];
                    if (!PhotonNetwork.IsMasterClient) continue;
                    GameManager.Instance.MahjongSplit(players.Count);
                    var a = JsonConvert.SerializeObject(GameManager.Instance.GetMahjongList());
                    var b = JsonConvert.SerializeObject(GameManager.Instance.GetUserMahjongLists());
                    for (var i = 1; i <= 4; i++)
                    {
                        if (i == myPlayerController.playerID)
                        {
                            continue;
                        }

                        GameObject.Find("PickPos" + i).SetActive(false);
                    }

                    photonView.RPC(nameof(SetList), RpcTarget.All, a, b);
                }
            }
        }

        /// <summary>
        /// 让主客户端每回合处理牌
        /// 0：无操作
        /// 1：碰
        /// 2：杠
        /// 3：胡
        /// 4：碰/杠
        /// 5：碰/赢
        /// 6：杠/赢
        /// 7：碰/杠/赢
        /// </summary>
        private void Update()
        {
            //按下有手柄的A等于做出旋转手势
            if (OVRInput.GetActiveController() == OVRInput.Controller.Touch && OVRInput.GetDown(OVRInput.Button.One))
            {
                SortMyMahjong(true, false);
            }

            //按下右手柄的B等于做出打开菜单的手势
            if (OVRInput.GetActiveController() == OVRInput.Controller.Touch && OVRInput.GetDown(OVRInput.Button.Two))
            {
                OpenOrClosePlayerCanvas();
            }

            if (!PhotonNetwork.IsMasterClient) return;
            //所有玩家在某人打出牌之后向主客户端汇报自己的状态（能否碰/杠/胡牌）
            //当字典的count等于玩家count，主客户端开始处理，否则锁死所有客户端
            if (ReadyDict.Count != _playerCount) return;
            //记录有多少玩家可以处理牌
            var flag = false;
            foreach (var item in ReadyDict)
            {
                switch (item.Value)
                {
                    case 0:
                        continue;
                    //该客户端可以处理牌
                    //给他处理
                    //可以碰牌
                    case 1:
                        photonView.RPC(nameof(CanP), RpcTarget.All, item.Key);
                        break;
                    //可以杠牌
                    case 2:
                        photonView.RPC(nameof(CanK), RpcTarget.All, item.Key);
                        break;
                    //可以胡牌
                    case 3:
                        photonView.RPC(nameof(CanH), RpcTarget.All, item.Key);
                        break;
                    case 4:
                        photonView.RPC(nameof(CanPAndK), RpcTarget.All, item.Key);
                        break;
                    //碰且赢
                    case 5:
                        photonView.RPC(nameof(CanPAndH), RpcTarget.All, item.Key);
                        break;
                    case 6:
                        photonView.RPC(nameof(CanKAndH), RpcTarget.All, item.Key);
                        break;
                    case 7:
                        photonView.RPC(nameof(CanPAndKAndH), RpcTarget.All, item.Key);
                        break;
                }

                //只要有一个人可以处理牌，就不应该继续发牌
                flag = true;
            }

            // 清空字典，准备下一回合
            ReadyDict.Clear();
            // 只要有一个人可以处理牌，就不应该继续发牌
            if (flag) return;
            // 牌打完了，荒庄
            if (GameManager.Instance.GetMahjongList().Count == 0)
            {
                photonView.RPC(nameof(NoOneWin), RpcTarget.All);
            }
            else
            {
                //下一回合，给下一位发牌
                photonView.RPC(nameof(NextTurn), RpcTarget.All,
                    nowTurn, true);
            }
        }

        private void DisableHandGrab()
        {
            foreach (var pair in myPlayerController.MyMahjong)
            {
                foreach (var mahjong in pair.Value)
                {
                    mahjong.GetComponent<HandGrabInteractable>().enabled = false;
                }
            }
        }

        private void EnableHandGrab()
        {
            foreach (var pair in myPlayerController.MyMahjong)
            {
                foreach (var mahjong in pair.Value)
                {
                    mahjong.GetComponent<HandGrabInteractable>().enabled = true;
                }
            }
        }

        [PunRPC]
        private void CanP(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(0).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            //if (myPlayerController.playerID != id) return;
            _canPong = true;
            DisableHandGrab();
        }

        [PunRPC]
        private void CanK(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(1).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            //if (myPlayerController.playerID != id) return;
            _canKong = true;
            DisableHandGrab();
        }

        [PunRPC]
        private void CanH(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(2).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            // if (myPlayerController.playerID != id) return;
            _canWin = true;
        }

        [PunRPC]
        private void CanPAndK(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(0).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(1).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            // if (myPlayerController.playerID != id) return;
            _canPong = true;
            _canKong = true;
            DisableHandGrab();
        }

        [PunRPC]
        private void CanPAndH(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(0).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(2).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            // if (myPlayerController.playerID != id) return;
            _canPong = true;
            _canWin = true;
            DisableHandGrab();
        }

        [PunRPC]
        private void CanKAndH(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(1).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(2).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            // if (myPlayerController.playerID != id) return;
            _canKong = true;
            _canWin = true;
            DisableHandGrab();
        }

        /// <summary>
        /// 可以碰，杠，胡
        /// </summary>
        /// <param name="id">可以碰杠胡的玩家的id</param>
        [PunRPC]
        private void CanPAndKAndH(int id)
        {
            if (myPlayerController.playerID != id) return;
            OpenPlayerButtonContainer();
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
            playerButtonContainers[myPlayerController.playerID - 1].GetChild(3).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(0).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(1).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(2).gameObject.SetActive(true);
            // playerButtonContainers[id - 1].GetChild(3).gameObject.SetActive(true);
            // if (myPlayerController.playerID != id) return;
            _canKong = true;
            _canKong = true;
            _canWin = true;
            DisableHandGrab();
        }

        /// <summary>
        /// 没有人胡牌，所有人显示流局
        /// </summary>
        [PunRPC]
        public void NoOneWin()
        {
            // var scoreCanvas = playerButtonContainers[myPlayerController.playerID - 1].GetChild(4).gameObject;
            // scoreCanvas.SetActive(true);
            OpenPlayerResultPanel();
            //scoreCanvas.transform.GetChild(1).GetComponentInChildren<TMP_Text>().text = "流局";
            playerResultPanels[myPlayerController.playerID - 1].GetChild(0).GetChild(0).GetComponent<TMP_Text>().text =
                "流局";
        }

        /// <summary>
        /// 排列手中麻将
        /// </summary>
        /// <param name="random">是否随机排列</param>
        /// <param name="disableCollider">排列的时候是否把所有牌的碰撞器禁用</param>
        public void SortMyMahjong(bool random, bool disableCollider)
        {
            var num = 1;
            //做出手势，随机摆放
            if (random)
            {
                foreach (var pair in myPlayerController.mahjongMap)
                {
                    pair.Value.Clear();
                }

                foreach (var pair in myPlayerController.MyMahjong)
                {
                    myPlayerController.mahjongMap[pair.Value.Count].Add(pair.Key);
                }

                foreach (var pair in myPlayerController.mahjongMap)
                {
                    var list = pair.Value.OrderBy(_ => rng.Next()).ToList();
                    for (var i = 0; i < pair.Value.Count; i++)
                    {
                        pair.Value[i] = list[i];
                    }
                }
            }
            

            for (var i = 4; i >= 1; i--)
            {
                myPlayerController.mahjongMap.TryGetValue(i, out var list);
                if (list == null) continue;
                list.Sort();
                foreach (var index in list)
                {
                    foreach (var go in myPlayerController.MyMahjong[index])
                    {
                        var script = go.GetComponent<MahjongAttr>();
                        if (script.num == 0)
                        {
                            continue;
                        }
            
                        BoxCollider boxCollider = null;
                        if (disableCollider)
                        {
                            boxCollider = go.GetComponent<BoxCollider>();
                            boxCollider.enabled = false;
                        }
            
                        script.num = num++;
                        var t = DOTween.Sequence()
                            .Insert(0f,
                                go.transform.DOMove(
                                    GameManager.Instance.GetPickPoses()[myPlayerController.playerID - 1].position +
                                    GameManager.Instance.GetBias()[myPlayerController.playerID - 1] *
                                    (script.num - 1),
                                    1f)).Insert(0f,
                                go.transform.DORotate(
                                    GameManager.Instance.GetRotateList()[myPlayerController.playerID - 1], 1f))
                            .SetEase(Ease.Linear);
                        t.onComplete += go.GetComponent<Rigidbody>().Sleep;
                        if (disableCollider)
                        {
                            t.onComplete += () => { boxCollider.enabled = true; };
                        }
                    }
                }
            }


            // foreach (var item in myPlayerController.MyMahjong)
            // {
            //     foreach (var go in item.Value)
            //     {
            //         var script = go.GetComponent<MahjongAttr>();
            //         if (script.num == 0)
            //         {
            //             continue;
            //         }
            //
            //         if (flag)
            //         {
            //             go.GetComponent<BoxCollider>().enabled = false;
            //         }
            //
            //         script.num = num++;
            //         var t = DOTween.Sequence()
            //             .Insert(0f,
            //                 go.transform.DOMove(
            //                     GameManager.Instance.GetPickPoses()[myPlayerController.playerID - 1].position +
            //                     GameManager.Instance.GetBias()[myPlayerController.playerID - 1] * (script.num - 1),
            //                     1f)).Insert(0f,
            //                 go.transform.DORotate(
            //                     GameManager.Instance.GetRotateList()[myPlayerController.playerID - 1], 1f))
            //             .SetEase(Ease.Linear);
            //         t.onComplete += go.GetComponent<Rigidbody>().Sleep;
            //         if (flag)
            //         {
            //             t.onComplete += () => { go.GetComponent<BoxCollider>().enabled = true; };
            //         }
            //     }
            // }
        }

        /// <summary>
        /// 做出旋转手势，触发随机排列麻将
        /// </summary>
        public void SortPose()
        {
            SortMyMahjong(true, false);
        }

        // 有人碰/杠的时候，直接清零
        // [PunRPC]
        // private void ResetPlayerDictCount()
        // {
        // 	_playerDictCount = 0;
        // }

        /// <summary>
        /// 处理碰牌
        /// </summary>
        private void SolvePong()
        {
            //自己能碰的时候，点击按钮才生效
            if (!_canPong) return;
            //点击之后立马不能碰牌
            _canPong = false;
            //向所有人RPC，当前轮次轮到我了，并且不需要发牌
            //photonView.RPC(nameof(ResetPlayerDictCount), RpcTarget.MasterClient);
            photonView.RPC(nameof(NextTurn), RpcTarget.All,
                myPlayerController.playerID, false);
            //向所有人RPC，隐藏所有按钮
            photonView.RPC(nameof(ResetButton), RpcTarget.All);
            //遍历自己的所有与被碰的牌相同的牌
            foreach (var go in myPlayerController.MyMahjong[nowTile])
            {
                var script = go.GetComponent<MahjongAttr>();
                //这些牌不能再被拿起来
                script.photonView.RPC(nameof(script.SetState), RpcTarget.All);
                go.GetComponent<HandGrabInteractable>().enabled = false;
                //把牌移动到指定的位置
                go.transform.DOMove(myPlayerController.putPos, 1f);
                //旋转牌
                go.transform.DORotate(GameManager.Instance.GetPlayerPutRotations()[myPlayerController.playerID - 1],
                    1f);
                //自己的位置减去一个牌的距离
                myPlayerController.putPos -= GameManager.Instance.GetBias()[myPlayerController.playerID - 1];
                script.num = 0;
                script.isPonged = true;
                script.inMyHand = false;
            }

            //再生成一个相同的牌，放到指定位置
            var newGo = PhotonNetwork.Instantiate("mahjong_tile_" + nowTile, myPlayerController.putPos,
                Quaternion.Euler(GameManager.Instance.GetPlayerPutRotations()[myPlayerController.playerID - 1]));
            var attr = newGo.GetComponent<MahjongAttr>();
            newGo.GetComponent<HandGrabInteractable>().enabled = false;
            attr.inMyHand = false;
            attr.num = 0;
            attr.isPonged = true;
            //这个牌也不能在被抓取
            attr.photonView.RPC(nameof(attr.SetState), RpcTarget.All);
            myPlayerController.MyMahjong[nowTile].Add(newGo);
            //整理牌
            SortMyMahjong(false, false);
            //销毁场上的那个牌
            photonView.RPC(nameof(DestroyItem), RpcTarget.All);
            myPlayerController.putPos -= GameManager.Instance.GetBias()[myPlayerController.playerID - 1];
            EnableHandGrab();
        }

        /// <summary>
        /// 杠分为杠别人和加杠
        /// </summary>
        private void SolveKong()
        {
            if (!_canKong) return;
            _canKong = false;
            
            // Start Edit
            _kongCount++;
            // End Edit
            
            //隐藏button
            photonView.RPC(nameof(ResetButton), RpcTarget.All);
            foreach (var pair in myPlayerController.MyMahjong)
            {
                if (pair.Value.Count == 4)
                {
                    var temp = pair.Value[0].GetComponent<MahjongAttr>();
                    //这种情况是加杠
                    if (temp.num == 0 && temp.isPonged)
                    {
                        pair.Value[3].transform
                            .DOMove(pair.Value[1].transform.position + pair.Value[1].transform.up * 0.5f, 1f);
                        pair.Value[3].transform.DORotate(
                            GameManager.Instance.GetPlayerPutRotations()[myPlayerController.playerID - 1], 1f);
                        var attr = pair.Value[3].GetComponent<MahjongAttr>();
                        attr.num = 0;
                        attr.inMyHand = false;
                        attr.GetComponent<HandGrabInteractable>().enabled = false;
                        foreach (var go in pair.Value)
                        {
                            go.GetComponent<MahjongAttr>().isPonged = false;
                            go.GetComponent<MahjongAttr>().isKonged = true;
                        }
                    }
                    //跳过已经杠过的
                    else if (temp.num == 0 && temp.isKonged)
                    {
                        continue;
                    }
                    //手上有4张牌，按顺序摆好
                    else
                    {
                        foreach (var go in pair.Value)
                        {
                            go.transform.DOMove(myPlayerController.putPos, 1f);
                            go.transform.DORotate(
                                GameManager.Instance.GetPlayerPutRotations()[myPlayerController.playerID - 1],
                                1f);
                            var attr = go.GetComponent<MahjongAttr>();
                            myPlayerController.putPos -=
                                GameManager.Instance.GetBias()[myPlayerController.playerID - 1];
                            attr.num = 0;
                            attr.inMyHand = false;
                            attr.isKonged = true;
                            go.GetComponent<HandGrabInteractable>().enabled = false;
                        }
                    }

                    SortMyMahjong(false, false);
                    //photonView.RPC(nameof(ResetPlayerDictCount), RpcTarget.MasterClient);
                    //拿到出牌权，看情况发牌
                    photonView.RPC(nameof(NextTurn), RpcTarget.All,
                        myPlayerController.playerID, true);
                    EnableHandGrab();
                    return;
                }
            }

            //杠牌
            foreach (var go in myPlayerController.MyMahjong[nowTile])
            {
                var script = go.GetComponent<MahjongAttr>();
                go.transform.DOMove(myPlayerController.putPos, 1f);
                go.transform.DORotate(GameManager.Instance.GetPlayerPutRotations()[myPlayerController.playerID - 1],
                    1f);
                myPlayerController.putPos -= GameManager.Instance.GetBias()[myPlayerController.playerID - 1];
                script.num = 0;
                script.inMyHand = false;
                script.isKonged = true;
                go.GetComponent<HandGrabInteractable>().enabled = false;
            }

            var newGo = PhotonNetwork.Instantiate("mahjong_tile_" + nowTile, myPlayerController.putPos,
                Quaternion.Euler(GameManager.Instance.GetPlayerPutRotations()[myPlayerController.playerID - 1]));
            myPlayerController.putPos -= GameManager.Instance.GetBias()[myPlayerController.playerID - 1];
            newGo.GetComponent<MahjongAttr>().num = 0;
            newGo.GetComponent<MahjongAttr>().inMyHand = false;
            newGo.GetComponent<MahjongAttr>().isKonged = true;
            newGo.GetComponent<HandGrabInteractable>().enabled = false;
            myPlayerController.MyMahjong[nowTile].Add(newGo);
            photonView.RPC(nameof(DestroyItem), RpcTarget.All);
            SortMyMahjong(false, false);
            //拿到出牌权，看情况发牌
            photonView.RPC(nameof(NextTurn), RpcTarget.All,
                myPlayerController.playerID, true);
            EnableHandGrab();
        }

        [PunRPC]
        public void DestroyItem()
        {
            GameObject destroyGo = null;
            foreach (var go in FindObjectsOfType<PhotonView>())
            {
                if (go.ViewID == tileViewID)
                {
                    destroyGo = go.gameObject;
                    break;
                }
            }

            Destroy(destroyGo);
        }
        //1：碰
        //2：杠
        //3：胡
        //4：碰/杠
        //5：碰/赢
        //6：杠/赢
        //7：碰/杠/赢

        public int CheckMyState(int id)
        {
            var ans = 0;
            if (myPlayerController.MyMahjong.ContainsKey(id))
            {
                //可以碰
                if (myPlayerController.MyMahjong[id].Count == 2)
                {
                    ans = 1;
                }

                //可以杠
                if (myPlayerController.MyMahjong[id].Count == 3 &&
                    myPlayerController.MyMahjong[id][0].GetComponent<MahjongAttr>().num != 0)
                {
                    //可以碰，也可以杠
                    if (ans == 1)
                    {
                        ans = 4;
                    }
                    //可以杠
                    else
                    {
                        ans = 2;
                    }
                }

                //是否赢了
                if (CheckWin(id))
                {
                    //可以赢，也可以碰
                    if (ans == 1)
                    {
                        ans = 5;
                    }
                    //可以杠，也可以赢
                    else if (ans == 2)
                    {
                        ans = 6;
                    }
                    //可以同时碰，杠，赢
                    else if (ans == 4)
                    {
                        ans = 7;
                    }
                    //只能赢
                    else
                    {
                        ans = 3;
                    }
                }
            }

            return ans;
        }

        /// <summary>
        /// 检测能否胡牌，id不传递的时候表示当前有14张，一般是检测自摸或者庄家开局检测能否胡牌
        /// </summary>
        /// <param name="id">待检测的牌id</param>
        /// <returns></returns>
        private bool CheckWin(int id = 0)
        {
            
            return CanWin(id);
        }
        

        /// <summary>
        /// 手牌组合包括顺子（3张牌），三个相同四个相同，以及一对牌的混合模式
        /// 采用对子法判断能够胡牌
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        private bool CanWin(int id = 0)
        {
            var tileCounts = new Dictionary<int, int>();
            int cnt2 = 0;

            // 统计每种牌的数量
            foreach (var item in myPlayerController.MyMahjong)
            {
                int tileType = item.Key;
                int tileCount = item.Value.Count;

                if (tileCounts.ContainsKey(tileType))
                {
                    tileCounts[tileType] += tileCount;
                }
                else
                {
                    tileCounts[tileType] = tileCount;
                }
            }

            // 增加新摸的牌
            if (tileCounts.ContainsKey(id))
            {
                // if (IsMajhongPongedOrKonged(id)) // 新加入的牌之前是被碰的，那么新牌只能单独用，提前清除被碰的三张牌
                // {
                //    tileCounts[id] = 1;
                // }
                // else
                // {
                //     tileCounts[id]++;
                // }
                tileCounts[id]++;
            }
            else
            {
                tileCounts[id] = 1;
            }

            // 尝试将每种牌作为对子，检查剩余牌是否可以组成顺子或刻子
            foreach (var pair in tileCounts.Keys.ToList())
            {
                
                if (tileCounts[pair] >= 2)
                {   
                    // 碰过或者杠过的不能拆开 直接删掉
                    // if (IsMajhongPongedOrKonged(pair))
                    // {
                    //     tileCounts[pair] = 0;
                    //     continue;
                    // }
                    //
                    cnt2++;
                    // 移除一对牌
                    tileCounts[pair] -= 2;

                    // 检查剩余牌是否可以组成顺子或刻子
                    if (IsWinningHand(tileCounts))
                    {
                        return true;
                    }

                    // 还原一对牌
                    tileCounts[pair] += 2;
                }
            }

            return cnt2 == 7;
        }

        /// <summary>
        /// 检查剩余牌是否可以组成顺子或刻子
        /// </summary>
        /// <param name="tileCounts"></param>
        /// <returns></returns>
        private bool IsWinningHand(Dictionary<int, int> tileCounts)
        {
            var counts = new Dictionary<int, int>(tileCounts);

            foreach (var key in counts.Keys.OrderBy(x => x).ToList())
            {
                
                if (counts[key] >= 3) 
                {
                    counts[key] -= 3;
                    if (counts[key] == 0) continue;
                }
                // 处理顺子
                if (key <= 7 || (key >= 10 && key <= 16) || (key >= 19 && key <= 25) || key == 32)
                {  
                    while (counts[key] > 0 && counts.ContainsKey(key + 1) && counts.ContainsKey(key + 2) &&
                           counts[key + 1] > 0 && counts[key + 2] > 0)
                    {
                        counts[key]--;
                        counts[key + 1]--;
                        counts[key + 2]--;
                    }
                }
                
            }
            
            // 处理東南西北风
            int[] fengTiles = { 28, 29, 30, 31 };
            for (int i = 0; i < fengTiles.Length - 2; i++)
            {
                for (int j = i + 1; j < fengTiles.Length - 1; j++)
                {
                    for (int k = j + 1; k < fengTiles.Length; k++)
                    {
                        if (counts.ContainsKey(fengTiles[i]) && counts.ContainsKey(fengTiles[j]) && counts.ContainsKey(fengTiles[k]) &&
                            counts[fengTiles[i]] > 0 && counts[fengTiles[j]] > 0 && counts[fengTiles[k]] > 0)
                        {
                            counts[fengTiles[i]]--;
                            counts[fengTiles[j]]--;
                            counts[fengTiles[k]]--;
                        }
                    }
                }
            }
            
            

            // 如果所有牌都被移除，则胡牌成功
            return counts.Values.All(cnt => cnt == 0);
        }

        /// <summary>
        /// 返回麻将是否被碰过或者杠过
        /// </summary>
        /// <param name="id"></param>
        public bool IsMajhongPongedOrKonged(int id)
        {
            var mahjongAttr = myPlayerController.MyMahjong[id][0].GetComponent<MahjongAttr>();
            return mahjongAttr.isPonged || mahjongAttr.isKonged;
        }
        
        /// <summary>
        /// 开启眼动追踪，实现透视效果
        /// </summary>
        public void ShowEyeGaze()
        {
            if (!GazeInteractor.startGaze)
            {
                PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
                    //在CallBack函数中打开UI，防止提前打开UI
                    data =>
                    {
                        //找到X射线卡牌的数量
                        var xRayCardCount = int.Parse(data.Data["XRayCard"].Value);
                        if (xRayCardCount > 0)
                        {
                            //更新数量
                            xRayCardCount--;
                            //更新显示
                            var xRayCardGo = playerCardContainers[myPlayerController.playerID - 1].GetChild(0);
                            xRayCardGo.GetComponentInChildren<TMP_Text>().text = $"透视道具\n当前拥有：{xRayCardCount}个";
                            //开启射线
                            GazeInteractor.startGaze = true;
                            //20秒后关闭射线
                            StartCoroutine(nameof(HideEyeGaze));
                            PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
                            {
                                Data = new Dictionary<string, string>
                                {
                                    { "XRayCard", xRayCardCount.ToString() }
                                }
                            }, _ => { }, _ => { });
                        }
                    },
                    error => { Debug.Log(error.ErrorMessage); });
            }
        }

        private IEnumerator HideEyeGaze()
        {
            yield return new WaitForSeconds(20f);
            GazeInteractor.startGaze = false;
            foreach (var mahjong in effectGoList)
            {
                mahjong.OnEyeHoverExit();
            }

            effectGoList.Clear();
        }


        public int count;

        /// <summary>
        /// 搓牌之后寻找能胡的牌，并换牌
        /// </summary>
        public void ChangeMahjong()
        {
            //count++;
            //if (count >= 3 && nowMahjong != null)

            PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
                //在CallBack函数中打开UI，防止提前打开UI
                data =>
                {
                    var cheatCardCount = int.Parse(data.Data["CheatCard"].Value);
                    if (cheatCardCount > 0)
                    {
                        //更新数量
                        cheatCardCount--;
                        //更新显示
                        var cheatCardGo = playerCardContainers[myPlayerController.playerID - 1].GetChild(1);
                        cheatCardGo.GetComponentInChildren<TMP_Text>().text = $"换牌道具\n当前拥有：{cheatCardCount}个";
                        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
                        {
                            Data = new Dictionary<string, string>
                            {
                                { "CheatCard", cheatCardCount.ToString() }
                            }
                        }, _ => { }, _ => { });
                    }
                },
                error => { Debug.Log(error.ErrorMessage); });
            StartCoroutine(Wait());
            //count = 0;
        }

        private IEnumerator Wait()
        {
            yield return new WaitForSeconds(3f);
            var id = nowMahjong.GetComponent<MahjongAttr>().ID;
            myPlayerController.mahjongMap[myPlayerController.MyMahjong[id].Count].Remove(id);
            myPlayerController.MyMahjong[id].Remove(nowMahjong);
            // if (myPlayerController.MyMahjong[id].Count == 0)
            // {
            //     myPlayerController.MyMahjong.Remove(id);
            // }

            var changeID = 0;
            for (var i = 1; i <= 34; i++)
            {
                if (CheckWin(i))
                {
                    changeID = i;
                    break;
                }
            }

            if (changeID != 0)
            {
                changeMahjongAudio.PlayAudio();
                nowMahjong.GetComponent<MahjongAttr>().ID = changeID;
                nowMahjong.GetComponent<MeshFilter>().mesh = GameManager.Instance.GetMahjongMesh(changeID);
                myPlayerController.mahjongMap[myPlayerController.MyMahjong[changeID].Count].Remove(changeID);
                myPlayerController.MyMahjong[changeID].Add(nowMahjong);
                myPlayerController.mahjongMap[myPlayerController.MyMahjong[changeID].Count].Add(changeID);
                photonView.RPC(nameof(CanH), RpcTarget.All, myPlayerController.playerID);
            }
            else
            {
                myPlayerController.mahjongMap[myPlayerController.MyMahjong[id].Count].Add(id);
                if (!myPlayerController.MyMahjong.ContainsKey(id))
                {
                    myPlayerController.MyMahjong[id] = new List<GameObject>();
                }

                myPlayerController.MyMahjong[id].Add(nowMahjong);
            }
        }

        /// <summary>
        /// 做出剪刀手势，打开或者菜单
        /// </summary>
        public void OpenOrClosePlayerCanvas()
        {
            if (!playerCanvases[myPlayerController.playerID - 1].gameObject.activeSelf &&
                !playerMenus[myPlayerController.playerID - 1].gameObject.activeSelf)
            {
                playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(true);
                playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
                questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            }

            else if (playerCanvases[myPlayerController.playerID - 1].gameObject.activeSelf &&
                     (playerCardContainers[myPlayerController.playerID - 1].gameObject.activeSelf ||
                      playerMenus[myPlayerController.playerID - 1].gameObject.activeSelf))
            {
                playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
                questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
                playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(false);
            }
        }

        private bool _firstQuest = true;

        public void OpenQuestPanel()
        {
            if (!_firstQuest)
            {
                return;
            }

            _firstQuest = false;
            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            questPanels[myPlayerController.playerID - 1].gameObject.SetActive(true);
            playerCapturers[myPlayerController.playerID - 1].gameObject.SetActive(true);
        }

        public void ToCapturer2()
        {
            StartCoroutine(nameof(ToChild2));
            // playerCapturers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
            // playerCapturers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
        }

        public IEnumerator ToChild2()
        {
            yield return new WaitForSeconds(2f);
            playerCapturers[myPlayerController.playerID - 1].GetChild(0).gameObject.SetActive(false);
            playerCapturers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(true);
        }

        public void ToCapturer3()
        {
            StartCoroutine(nameof(ToChild3));
            // playerCapturers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
            // playerCapturers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
        }

        public IEnumerator ToChild3()
        {
            yield return new WaitForSeconds(2f);
            playerCapturers[myPlayerController.playerID - 1].GetChild(1).gameObject.SetActive(false);
            playerCapturers[myPlayerController.playerID - 1].GetChild(2).gameObject.SetActive(true);
        }

        public void GetReward()
        {
            StartCoroutine(nameof(CoroGetReward));
        }

        public IEnumerator CoroGetReward()
        {
            yield return new WaitForSeconds(2f);
            PlayFabClientAPI.GetUserData(new GetUserDataRequest(),
                data =>
                {
                    var cardCount = int.Parse(data.Data["XRayCard"].Value);
                    PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest()
                        {
                            Data = new Dictionary<string, string>
                            {
                                { "XRayCard", (cardCount + 1).ToString() }
                            }
                        }, _ => { },
                        _ => { }
                    );
                },
                error => { Debug.Log(error.ErrorMessage); });


            playerCanvases[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerMenus[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerResultPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerButtonContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCardContainers[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerPunishPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            questPanels[myPlayerController.playerID - 1].gameObject.SetActive(false);
            playerCapturers[myPlayerController.playerID - 1].gameObject.SetActive(false);
        }

        /// <summary>
        /// 惩罚玩家
        /// </summary>
        public void PunishPlayer(int id)
        {
            recordingCamera.LookAt = GameObject.Find($"Player{id}").transform;
        }
    }
}
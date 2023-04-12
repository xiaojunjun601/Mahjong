using System.Linq;
using Controller;
using Manager;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Photon.Pun;
using UnityEngine;

public class MahjongAttr : MonoBehaviourPunCallbacks
{
    private readonly Vector3 _pos = Vector3.zero;
    private PhotonView _photonView;
    public int id;
    public int num;
    private PhotonView _gameManagerPhotonView;
    private PlayerController _myPlayerController;
    public bool canPlay = true;
    private Vector3 _moveto;
    private Vector3 _rotateTo;
    private bool _isGrounded = true;
    public Transform parentTrans;
    public bool isSet = false;
    private Rigidbody _rigidbody;
    public PointableUnityEventWrapper pointableUnityEventWrapper;
    private HandGrabInteractable[] _handGrabInteractable;
    public bool inHand = false;
    public bool isPut = true;
    public bool isAdd = false;
    public Vector3 originPosition;
    public Quaternion originalRotation;

    private void Awake()
    {
        _gameManagerPhotonView = GameManager.Instance.GetComponent<PhotonView>();
        _rigidbody = GetComponent<Rigidbody>();
        _myPlayerController = GameController.Instance.myPlayerController;
        _handGrabInteractable = GetComponentsInChildren<HandGrabInteractable>();
        //id = int.Parse(name[..^7][13..]);
        pointableUnityEventWrapper = GetComponent<PointableUnityEventWrapper>();
        pointableUnityEventWrapper.InjectAllPointableUnityEventWrapper(GetComponent<Grabbable>());
        _photonView = GetComponent<PhotonView>();
        pointableUnityEventWrapper.WhenSelect.AddListener(OnGrab);
        pointableUnityEventWrapper.WhenUnselect.AddListener(OnPut);
        var transform1 = transform;
        originPosition = transform1.position;
        originalRotation = transform1.rotation;
        isPut = true;
        // GetComponent<XRGrabInteractable>().hoverEntered.AddListener(_ => { OnHover(); });
        // GetComponent<XRGrabInteractable>().hoverExited.AddListener(_ => { OnHoverExit(); });
        // GetComponent<XRGrabInteractable>().activated.AddListener(_ => { OnTrigger(); });
        //GetComponent<XRGrabInteractable>().firstSelectEntered.AddListener(_ => { OnGrab(); });
    }

    public void OnGrab()
    {
        _photonView.RPC(nameof(SetKinematic), RpcTarget.All, true);
    }

    public void OnPut()
    {
        _photonView.RPC(nameof(SetKinematic), RpcTarget.All, false);
        if (isPut)
        {
            _myPlayerController.MyMahjong[id].RemoveAt(0);
            GameController.Instance.photonView.RPC(nameof(GameController.Instance.NextTurn), RpcTarget.All,
                _myPlayerController.playerID == PhotonNetwork.CurrentRoom.PlayerCount
                    ? 1
                    : _myPlayerController.playerID + 1);
        }
    }

    [PunRPC]
    private void SetKinematic(bool b)
    {
        _rigidbody.isKinematic = b;
    }

    [PunRPC]
    public void SetState(bool b)
    {
        foreach (var handGrabInteractable in _handGrabInteractable)
        {
            handGrabInteractable.enabled = b;
        }
    }
    // private void OnHover()
    // {
    //     if (!photonView.IsMine) return;
    //     if (!canPlay) return;
    //     //transform1.localScale = new Vector3(3f, 3f, 3f);
    //     if (!_isGrounded) return;
    //     transform.position += new Vector3(0f, 1f, 0f);
    //     _isGrounded = false;
    // }
    //
    // private void OnHoverExit()
    // {
    //     if (!photonView.IsMine) return;
    //     if (!canPlay) return;
    //     if (_isGrounded) return;
    //     //transform1.localScale = new Vector3(2f, 2f, 2f);
    //     transform.position -= new Vector3(0f, 1f, 0f);
    //     _isGrounded = true;
    // }
    //
    // private void OnGrab()
    // {
    //     if(isSet) return;
    //     var go = new GameObject();
    //     go.transform.position = gameObject.transform.position;
    //     go.transform.SetParent(parentTrans);
    //     parentTrans.GetComponent<XRSocketInteractor>().attachTransform = go.transform;
    //     isSet = true;
    // }

    // private void OnTrigger()
    // {
    //     if (!photonView.IsMine) return;
    //     if (!canPlay) return;
    //     if (!GameController.Instance.myPlayerController.isMyTurn) return;
    //     GetComponent<BoxCollider>().isTrigger = true;
    //     var transform1 = transform;
    //     _myPlayerController.PlayTileStrategy.MahjongPut(transform1);
    //     _myPlayerController.PlayTileStrategy.MahjongRotate(transform1);
    //     //_gameManagerPhotonView.RPC(nameof(GameManager.instance.SendID), RpcTarget.Others,id);
    //     if (GameController.Instance.myPlayerController.MyMahjong[id].Count == 1)
    //     {
    //         GameController.Instance.myPlayerController.MyMahjong.Remove(id);
    //     }
    //     else
    //     {
    //         GameObject t = null;
    //         foreach (var iGameObject in GameController.Instance.myPlayerController.MyMahjong[id])
    //         {
    //             if (iGameObject.GetComponent<MahjongAttr>().num == num)
    //             {
    //                 t = iGameObject;
    //             }
    //         }
    //
    //         if (t != null)
    //         {
    //             GameController.Instance.myPlayerController.MyMahjong[id].Remove(t);
    //         }
    //     }
    //
    //     GameController.Instance.SortMyMahjong();
    //     //我打出一张牌
    //     _gameManagerPhotonView.RPC(nameof(GameManager.Instance.PlayTile), RpcTarget.All, id,
    //         GameController.Instance.myPlayerController.playerID);
    //     GameController.Instance.NotMyTurn();
    //     //_gameManagerPhotonView.RPC(nameof(GameManager.instance.StoreTile), RpcTarget.MasterClient, gameObject);
    //     GameController.Instance.tile = gameObject;
    // }
}
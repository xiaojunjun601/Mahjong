using System.Collections.Generic;
using System.Linq;
using Controller;
using Manager;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using Photon.Pun;
using Unity.Mathematics;
using UnityEngine;

public class MahjongAttr : MonoBehaviourPunCallbacks
{
    private PhotonView _photonView;

    /// <summary>
    /// 麻将牌的ID标识符，每种ID对应一种麻将
    /// </summary>
    [HideInInspector] public int ID;

    /// <summary>
    ///  麻将在自己手牌中的序号，用于系统自动摆牌时确定位置
    /// </summary>
    [HideInInspector] public int num;

    private Rigidbody _rigidbody;

    [HideInInspector] public PointableUnityEventWrapper pointableUnityEventWrapper;

    //private HandGrabInteractable[] _handGrabInteractable;
    private HandGrabInteractable _handGrabInteractable;
    private TouchHandGrabInteractable _touchHandGrabInteractable;
    private PhysicMaterial _physicMaterial;

    /// <summary>
    /// 是否在自己手中，如果在自己手中，这个bool为true
    /// </summary>
    public bool inMyHand;

    /// <summary>
    /// 是否在他人手中，如果在他人手中，这个bool为true
    /// </summary>
    public bool inOthersHand;

    /// <summary>
    /// 是否在可以打出的区域，如果在可以打出的区域，这个bool为true，此时松手牌视为被打出，这个bool由场景中的碰撞体来确定
    /// </summary>
    public bool isPut;

    /// <summary>
    /// 是否把牌拿入自己的牌堆，用于检测玩家把桌上的牌拿到自己的牌堆的时候，把牌放回原来的位置
    /// </summary>
    public bool isAdd;

    /// <summary>
    /// 是否摆在桌子上，如果没有被任何人拿到手中，且没有被任何人打出，这个bool为true
    /// </summary>
    public bool onDesk;

    /// <summary>
    /// 是否已经被打出，如果被任何人打出，这个bool为true
    /// </summary>
    public bool isThrown;

    /// <summary>
    /// 是否被碰过
    /// </summary>
    public bool isPonged;

    /// <summary>
    /// 是否被杠过
    /// </summary>
    public bool isKonged;

    public bool flag = false;
    private Vector3 _originalPos;
    private GameObject _effectGo;
    private GameObject _bubbleGo;
    private Transform _transform;
    private MeshRenderer _meshRenderer;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _meshRenderer = GetComponent<MeshRenderer>();
        var grabable = GetComponent<Grabbable>();
        //var boxCollider = GetComponent<BoxCollider>();
        _physicMaterial = GetComponent<PhysicMaterial>();
        //_handGrabInteractable = GetComponentsInChildren<HandGrabInteractable>();
        _handGrabInteractable = GetComponent<HandGrabInteractable>();
        _handGrabInteractable.InjectOptionalPhysicsGrabbable(GetComponent<PhysicsGrabbable>());
        // _touchHandGrabInteractable = GetComponent<TouchHandGrabInteractable>();
        // _touchHandGrabInteractable.InjectOptionalPointableElement(grabable);
        // var colliders = new List<Collider> {boxCollider};
        // _touchHandGrabInteractable.InjectAllTouchHandGrabInteractable(boxCollider, colliders);
        pointableUnityEventWrapper = GetComponent<PointableUnityEventWrapper>();
        pointableUnityEventWrapper.InjectAllPointableUnityEventWrapper(grabable);
        _photonView = photonView;
        pointableUnityEventWrapper.WhenSelect.AddListener(OnGrab);
        pointableUnityEventWrapper.WhenUnselect.AddListener(OnPut);
        _transform = transform;
    }

    public void OnPut(PointerEvent evt)
    {
        _photonView.RPC(nameof(SetKinematic), RpcTarget.All, false);
    }

    public void OnGrab(PointerEvent evt)
    {
        _photonView.RPC(nameof(SetKinematic), RpcTarget.Others, true);
        //如果玩家尝试拿起界外的牌，先记录自己的位置，然后当玩家扔牌的时候，如果扔到自己的手牌，强制归位
        if (isPut && isThrown)
        {
            var position = _transform.position;
            _originalPos = new Vector3(position.x, position.y + 2f, position.z);
        }

        if (GameController.Instance.nowMahjong == null)
        {
            GameController.Instance.count = 0;
        }

        GameController.Instance.nowMahjong = gameObject;
    }

    // public void OnGrab()
    // {
    //     _photonView.RPC(nameof(SetKinematic), RpcTarget.Others, true);
    //     //如果玩家尝试拿起界外的牌，先记录自己的位置，然后当玩家扔牌的时候，如果扔到自己的手牌，强制归位
    //     if (isPut && isThrown)
    //     {
    //         var position = _transform.position;
    //         _originalPos = new Vector3(position.x, position.y + 2f, position.z);
    //     }
    // }
    //
    // public void OnPut()
    // {
    //     _photonView.RPC(nameof(SetKinematic), RpcTarget.All, false);
    // }

    /// <summary>
    /// 设置当前的Rigidbody是否为运动学的
    /// </summary>
    /// <param name="isKinematic">是否是运动学的，当被某人拿在手里，设置为false，否则为true</param>
    [PunRPC]
    private void SetKinematic(bool isKinematic)
    {
        _rigidbody.isKinematic = isKinematic;
    }

    /// <summary>
    /// 设置自己的麻将不能被其他人抓取
    /// </summary>
    [PunRPC]
    public void SetState()
    {
        //对所有其他人，麻将不能抓取
        // foreach (var handGrabInteractable in _handGrabInteractable)
        // {
        //     handGrabInteractable.enabled = false;
        // }
        _handGrabInteractable.enabled = false;
    }

    /// <summary>
    /// 设置麻将为在自己手中的状态
    /// </summary>
    /// <param name="flag">是否在手中，如果在自己手中，inMyHand为true</param>
    [PunRPC]
    public void RPCSetInMyHand(bool flag)
    {
        inMyHand = flag;
    }

    /// <summary>
    /// 设置麻将为在他人手中的状态
    /// </summary>
    /// <param name="flag">是否在手中，如果在他人手中，inOtherHand为true</param>
    [PunRPC]
    public void RPCSetInOthersHand(bool flag)
    {
        inOthersHand = flag;
    }

    /// <summary>
    /// 设置麻将为已经被扔出的状态
    /// </summary>
    /// <param name="flag">是否已经被打出，如果被任何人打出，这个bool为true</param>
    [PunRPC]
    public void RPCSetIsThrown(bool flag)
    {
        isThrown = flag;
    }

    /// <summary>
    /// 设置麻将为摆在桌上的状态
    /// </summary>
    /// <param name="flag"></param>
    [PunRPC]
    public void RPCSetOnDesk(bool flag)
    {
        onDesk = flag;
    }

    /// <summary>
    /// 设置当前的Layer（用于射线检测）
    /// </summary>
    /// <param name="layer"></param>
    [PunRPC]
    public void RPCSetLayer(int layer)
    {
        gameObject.layer = layer;
    }

    [PunRPC]
    private void PlayTile(int playerId, int tileId, int viewID)
    {
        //每个客户端先把把当前轮次的ID设置好（下面代码可能会更改）
        GameController.Instance.nowTurn = playerId == PhotonNetwork.CurrentRoom.PlayerCount
            ? 1
            : playerId + 1;
        //每个客户端先把把当前轮次的牌ID设置好（下面代码可能会更改）
        GameController.Instance.nowTile = tileId;
        GameController.Instance.tileViewID = viewID;
        var thisID = GameController.Instance.myPlayerController.playerID;
        //打出牌的一定准备好了
        if (playerId == thisID)
        {
            //是主客户端，直接加入
            if (PhotonNetwork.IsMasterClient)
            {
                GameController.Instance.ReadyDict.Add(playerId, 0);
            }
            //向主客户端发送自己的状态
            else
            {
                _photonView.RPC(nameof(Send), RpcTarget.MasterClient, playerId, 0);
            }
        }
        else
        {
            //check自己的状态
            var flag = GameController.Instance.CheckMyState(tileId);
            //是主客户端，直接加入
            if (PhotonNetwork.IsMasterClient)
            {
                GameController.Instance.ReadyDict.Add(
                    GameController.Instance.myPlayerController.playerID, flag);
            }
            //向主客户端发送自己的状态
            else
            {
                _photonView.RPC(nameof(Send), RpcTarget.MasterClient,
                    GameController.Instance.myPlayerController.playerID, flag);
            }
        }
    }

    [PunRPC]
    public void Send(int playerId, int flag)
    {
        GameController.Instance.ReadyDict.Add(playerId, flag);
    }

    /// <summary>
    /// 其他人接触自己牌的时候，生成护盾特效
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerEnter(Collider other)
    {
        if (inOthersHand && other.gameObject.CompareTag("Hand") && _bubbleGo == null)
        {
            _bubbleGo = Instantiate(GameController.Instance.bubbleEffect, transform.position, quaternion.identity);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_bubbleGo != null)
        {
            Destroy(_bubbleGo.gameObject);
        }
    }

    /// <summary>
    /// 基于碰撞检测判断玩家是否出牌成功，如果碰到桌子，说明出牌成功，否则弹回原始位置
    /// </summary>
    /// <param name="other"></param>
    private void OnCollisionEnter(Collision other)
    {
        //落在桌子上
        if (other.gameObject.CompareTag("Desk"))
        {
            if (GameController.Instance.nowMahjong == gameObject)
            {
                GameController.Instance.nowMahjong = null;
                GameController.Instance.count = 0;
            }

            if (inMyHand && isPut)
            {
                var playerController = GameController.Instance.myPlayerController;
                var playerId = playerController.playerID;
                //可以出牌，先把牌移除，再整理牌
                if (GameController.Instance.nowTurn == playerId)
                {
                    GameObject go = null;
                    foreach (var item in playerController.MyMahjong[ID])
                    {
                        if (item.GetComponent<MahjongAttr>().num == num)
                        {
                            go = item;
                        }
                    }

                    if (go != null)
                    {
                        playerController.mahjongMap[playerController.MyMahjong[ID].Count].Remove(ID);
                        playerController.MyMahjong[ID].Remove(go);
                        playerController.mahjongMap[playerController.MyMahjong[ID].Count].Add(ID);
                    }

                    GameController.Instance.SortMyMahjong(false, false);
                    isThrown = true;
                    inMyHand = false;
                    inOthersHand = false;
                    onDesk = false;
                    if (!_photonView.IsMine)
                    {
                        GetComponent<MeshFilter>().mesh = GameManager.Instance.GetMahjongMesh(ID);
                    }

                    gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
                    GameController.Instance.GazeInteractor.startCount = false;
                    _photonView.RPC(nameof(PlayTile), RpcTarget.All, playerId, ID, gameObject.GetPhotonView().ViewID);
                    _photonView.RPC(nameof(RPCSetIsThrown), RpcTarget.Others, true);
                    _photonView.RPC(nameof(RPCSetInMyHand), RpcTarget.Others, false);
                    _photonView.RPC(nameof(RPCSetInOthersHand), RpcTarget.Others, false);
                    _photonView.RPC(nameof(RPCSetOnDesk), RpcTarget.Others, false);
                    _photonView.RPC(nameof(RPCSetLayer), RpcTarget.Others, LayerMask.NameToLayer("Ignore Raycast"));
                }
                //本回合不能出牌，直接整理牌
                else
                {
                    GameController.Instance.SortMyMahjong(false, false);
                }

                //当玩家尝试把桌子上的牌拿到手牌，强制归位
                if (isAdd && isThrown)
                {
                    if (GameController.Instance.nowMahjong == gameObject)
                    {
                        GameController.Instance.nowMahjong = null;
                        GameController.Instance.count = 0;
                    }

                    _transform.position = _originalPos;
                }
            }
        }
        //落在地上
        else if (other.gameObject.CompareTag("Ground") && inMyHand && isPut)
        {
            GameController.Instance.SortMyMahjong(false, true);
        }
    }

    public void OnEyeHoverEnter()
    {
        if (inOthersHand)
        {
            var materials = _meshRenderer.materials;
            materials[0] = GameController.Instance.transparentMaterials[0];
            materials[1] = GameController.Instance.transparentMaterials[1];
            _meshRenderer.materials = materials;
            _effectGo = Instantiate(GameController.Instance.effectPrefab, _transform.position, _transform.rotation);
            GameController.Instance.effectGoList.Add(this);
        }
    }

    public void OnEyeHoverExit()
    {
        if (inOthersHand && _effectGo != null)
        {
            var materials = _meshRenderer.materials;
            materials[0] = GameController.Instance.normalMaterials[0];
            materials[1] = GameController.Instance.normalMaterials[1];
            _meshRenderer.materials = materials;
            Destroy(_effectGo);
            GameController.Instance.effectGoList.Remove(this);
        }
    }

    // public void DestroyEffectGameObject()
    // {
    //     if (_effectGo == null) return;
    //     var materials = _meshRenderer.materials;
    //     materials[0] = GameController.Instance.normalMaterials[0];
    //     materials[1] = GameController.Instance.normalMaterials[1];
    //     _meshRenderer.materials = materials;
    //     Destroy(_effectGo);
    // }
}
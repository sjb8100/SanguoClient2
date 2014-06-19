﻿/// <summary>
/// Author : dandanshih
/// Date : 2014/6/16
/// </summary>

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Threading;

// 所使用到的常數
public partial class Const
{
	// 連線的 Http 位置
	// [problem] 以後應該要讀表或是從下載回來的資料得知
	public static string ServerURL = "http://localhost/SG_Login/GameService.asmx";
}

// 每一個被放進來的 Client 行為
public class CClientActionData
{
	// 那個 ClientAction
	public ClientActionID m_CID;
	// ClientAction 所需要的參數 
	public Dictionary<string, object> m_Args;
	// 不應該被使用的
	public Dictionary<string, object> m_dictProtocol;
	// 額外參數
	public object m_State;
	
	// 建構子
	public CClientActionData 
		(
			string ActionName
			, Dictionary<string, object> Args
			, Dictionary<string, object> dictProtocol
			, object State
			)
	{
		m_CID = (ClientActionID) System.Convert.ToInt32 (ActionName);
		m_Args = Args;
		m_dictProtocol = dictProtocol;
		m_State = State;
	}
}

// 開一條連線, 重覆一直用
public partial class GameService : Singleton<GameService>
{
	//  做 Thread Lock 用
	public static Mutex m_Mutex = new Mutex ();

	private GameService ()
	{
	}

	// 統一回來的處理
	public static void ProtocolCompleteCallback(ErrorType errorCode, object result, object userState, WebPost.CReqState state)
	{
		LogMgr.DebugLog ("[GameService][ProtocolCompleteCallback] errorCode:{0}, result:{1}, userState:{2}, state:{3}", errorCode, result, userState, state);
		// 檢查是不是正確
		if (errorCode == ErrorType.Error)
		{
			LogMgr.ErrorLog ("[GameService][ProtocolCompleteCallback] Error!! errorCode:{0}, result:{1}, userState:{2}, state:{3}", errorCode, result, userState, state);
			return;
		}
		else if (errorCode == ErrorType.Timeout)
		{
			// 做重送的動作?!
			LogMgr.ErrorLog ("[GameService][ProtocolCompleteCallback] Timeout!! errorCode:{0}, result:{1}, userState:{2}, state:{3}", errorCode, result, userState, state);
			return;
		}
		else
		{
			// 取得參數
			Dictionary<string, object> dictResult = JsonConvert.DeserializeObject<Dictionary<string, object>> (result.ToString());
			dictResult = JsonConvert.DeserializeObject<Dictionary<string, object>> ( dictResult["d"].ToString());
			//Debug.Log (JsonConvert.SerializeObject( dictResult));
			ErrorID eid = (ErrorID)System.Convert.ToInt32 (dictResult["Result"]);
			if (eid != ErrorID.Success)
			{
				LogMgr.ErrorLog ("[GameService][ProtocolCompleteCallback] Parser Error, ErrorID = {0}, Msg={1}", eid, IDMap.GetEnumAttribute (eid));
			}
			// 取得 ClientAction
			if (dictResult.ContainsKey ("ClientAction") == false)
				return;
			List<KeyValuePair<string, object>> listAction = JsonConvert.DeserializeObject<List<KeyValuePair<string, object>>> (dictResult["ClientAction"].ToString());
			foreach (var ChildAction in listAction)
			{
				Dictionary<string, object> dictArgs = ChildAction.Value as Dictionary<string, object>;
				try
				{
					DoClientAction (ChildAction.Key, dictArgs, dictResult, userState);
				}
				catch (Exception e)
				{
					LogMgr.ErrorLog ("[Error!!] {0}", e.ToString());
				}
			}
		}
	}
	
	// Update is called once per frame
	#region ClientAction 處理區
	static List<CClientActionData> m_ClientAction = new List<CClientActionData> ();

	// 取得 ClientAction
	public static void DoClientAction (string ClientActionName, Dictionary<string, object> Args, Dictionary<string, object> dictProtocol, object State)
	{
		LogMgr.DebugLog ("[GameService.DoClientAction] ClientActionName:{0}, Args:{1}", ClientActionName, JsonConvert.SerializeObject(Args));
		ModifyClientAction (new CClientActionData (ClientActionName, Args, dictProtocol, State));
	}

	// 塞入一個 / 取出一個
	static CClientActionData ModifyClientAction (CClientActionData Action = null)
	{
		m_Mutex.WaitOne ();
		// 塞入一個
		if (Action != null)
		{
			m_ClientAction.Add (Action);
			m_Mutex.ReleaseMutex ();
			return null;
		}
		// 取出一個
		CClientActionData Data = null;
		if (m_ClientAction.Count > 0)
		{
			Data = m_ClientAction[0];
			m_ClientAction.RemoveAt (0);
		}
		m_Mutex.ReleaseMutex ();
		return Data;
	}
	// 計算數量

	// 更新
	public void Update () 
	{
		// 沒有 Client Action 不需要處理
		if (m_ClientAction.Count == 0)
			return;
		// 取得要 Update 的資料
		CClientActionData Data = ModifyClientAction ();
		if (Data == null)
			return;
		// 創角
		if (Data.m_CID == ClientActionID.ToNewPlayer)
		{
			// 跳視窗說還沒有做
		}
		// 做登入動作
		else if (Data.m_CID == ClientActionID.ToLogin)
		{
			// 先把 Login 面版關閉
			GameUtility.HideUI (Const.Panel_Login);
			// 記錄帳號 (Memory)
			string Account = Data.m_dictProtocol["Account"].ToString();
			PlayerAttr.Account = Account;
			// 記錄 AccountID
			int AccountID = System.Convert.ToInt32 (Data.m_dictProtocol["AccountID"].ToString());
			PlayerAttr.AccountID = AccountID;
			// 記錄 PlayerID
			int PlayerID = System.Convert.ToInt32 (Data.m_dictProtocol["PlayerID"].ToString());
			PlayerAttr.PlayerID = PlayerID;
			// 記錄 SessionKey
			string SessionKey = Data.m_dictProtocol["SessionKey"].ToString();
			PlayerAttr.SessionKey = SessionKey;
			// 記錄密碼
			ClientSaveMgr.SetString (Const.Input_Account, Account);
			ClientSaveMgr.SetString (Const.Input_Password, Data.m_dictProtocol["Password"].ToString());
			// 拉出 Panel
			GameUtility.ShowUI (Const.Panel_Main);
			// 從網路要資料
			GameService.Player_GetAttr ();
		}
	}

	#endregion

}

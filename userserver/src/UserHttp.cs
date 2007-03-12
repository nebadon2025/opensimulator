/*
Copyright (c) OpenGrid project, http://osgrid.org/


* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.Text;
using Nwc.XmlRpc;
using System.Threading;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;
using ServerConsole;

namespace OpenGridServices
{
	public class UserHTTPServer {
		public Thread HTTPD;
		public HttpListener Listener;
	
		public UserHTTPServer() {
	 		ServerConsole.MainConsole.Instance.WriteLine("Starting up HTTP Server");
			HTTPD = new Thread(new ThreadStart(StartHTTP));
			HTTPD.Start();
		}

		public void StartHTTP() {
			ServerConsole.MainConsole.Instance.WriteLine("UserHttp.cs:StartHTTP() - Spawned main thread OK");
			Listener = new HttpListener();

			Listener.Prefixes.Add("http://+:8002/userserver/");
			Listener.Start();

			HttpListenerContext context;
			while(true) {
				context = Listener.GetContext();
				ThreadPool.QueueUserWorkItem(new WaitCallback(HandleRequest), context);
			}
		}

		static string ParseXMLRPC(string requestBody) {
			XmlRpcRequest request = (XmlRpcRequest)(new XmlRpcRequestDeserializer()).Deserialize(requestBody);
		
			Hashtable requestData = (Hashtable)request.Params[0];
			switch(request.MethodName) {
				case "login_to_simulator":
					bool GoodXML= (requestData.Contains("first") && requestData.Contains("last") && requestData.Contains("passwd"));
					bool GoodLogin=false;
					string firstname="";
					string lastname="";
					string passwd="";
					
					if(GoodXML) {
						firstname=(string)requestData["first"];
						lastname=(string)requestData["last"];
						passwd=(string)requestData["passwd"];
						GoodLogin=OpenUser_Main.userserver._profilemanager.AuthenticateUser(firstname,lastname,passwd);
					}

					
					if(!(GoodXML && GoodLogin)) {
						XmlRpcResponse LoginErrorResp = new XmlRpcResponse();
						Hashtable ErrorRespData = new Hashtable();
						ErrorRespData["reason"]="key";
						ErrorRespData["message"]="Error connecting to grid. Please double check your login details and check with the grid owner if you are sure these are correct";
						ErrorRespData["login"]="false";
						LoginErrorResp.Value=ErrorRespData;
						return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(LoginErrorResp)," encoding=\"utf-16\"","" ));
					} 
					
					UserProfile TheUser=OpenUser_Main.userserver._profilemanager.GetProfileByName(firstname,lastname);
					
					if(!((TheUser.CurrentSessionID==null) && (TheUser.CurrentSecureSessionID==null))) {
                                                XmlRpcResponse PresenceErrorResp = new XmlRpcResponse();
                                                Hashtable PresenceErrorRespData = new Hashtable();
                                                PresenceErrorRespData["reason"]="presence";
                                                PresenceErrorRespData["message"]="You appear to be already logged in, if this is not the case please wait for your session to timeout, if this takes longer than a few minutes please contact the grid owner";
                                                PresenceErrorRespData["login"]="false";
                                                PresenceErrorResp.Value=PresenceErrorRespData;
                                                return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(PresenceErrorResp)," encoding=\"utf-16\"","" ));

					}
					
				try {	
					LLUUID AgentID = TheUser.UUID;
					TheUser.InitSessionData();
					SimProfile SimInfo = new SimProfile();
					SimInfo = SimInfo.LoadFromGrid(TheUser.homeregionhandle,OpenUser_Main.userserver.GridURL,OpenUser_Main.userserver.GridSendKey,OpenUser_Main.userserver.GridRecvKey);

					XmlRpcResponse LoginGoodResp = new XmlRpcResponse();
					Hashtable LoginGoodData = new Hashtable();
				
					Hashtable GlobalT = new Hashtable();
					GlobalT["sun_texture_id"] = "cce0f112-878f-4586-a2e2-a8f104bba271";
					GlobalT["cloud_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
					GlobalT["moon_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
					ArrayList GlobalTextures = new ArrayList();
					GlobalTextures.Add(GlobalT);

					Hashtable LoginFlagsHash = new Hashtable();
					LoginFlagsHash["daylight_savings"]="N";
					LoginFlagsHash["stipend_since_login"]="N";
					LoginFlagsHash["gendered"]="Y";
					LoginFlagsHash["ever_logged_in"]="Y";
					ArrayList LoginFlags=new ArrayList();
					LoginFlags.Add(LoginFlagsHash);

					Hashtable uiconfig = new Hashtable();
					uiconfig["allow_first_life"]="Y";
					ArrayList ui_config=new ArrayList();
					ui_config.Add(uiconfig);

					LoginGoodData["message"]=OpenUser_Main.userserver.DefaultStartupMsg;
					LoginGoodData["session_id"]=TheUser.CurrentSessionID.ToString();
					LoginGoodData["secure_sessionid"]=TheUser.CurrentSecureSessionID.ToString();
					LoginGoodData["agent_access"]="M";
					LoginGoodData["start_location"]=requestData["start"];
					LoginGoodData["look_at"]="[r" + TheUser.homelookat.X.ToString() + ", r" + TheUser.homelookat.Y.ToString() + ", r" + TheUser.homelookat.Z.ToString() + "]";
					LoginGoodData["global_textures"]=GlobalTextures;
					LoginGoodData["seconds_since_epoch"]=(Int32)(DateTime.UtcNow - new DateTime(1970,1,1)).TotalSeconds;
					LoginGoodData["firstname"]=firstname;
					LoginGoodData["circuit_code"]=(new Random()).Next().ToString();
					LoginGoodData["login_flags"]=LoginFlags;
					LoginGoodData["seed_capability"]=SimInfo.caps_url;
					LoginGoodData["home"]="{'region_handle':'r" + (SimInfo.RegionLocX*256).ToString() + "," + (SimInfo.RegionLocY*256).ToString() + "', 'position':'r" + TheUser.homepos.X.ToString() + "," + TheUser.homepos.Y.ToString() + "," + TheUser.homepos.Z.ToString() + "', 'look_at':'r" + TheUser.homelookat.X.ToString() + "," + TheUser.homelookat.Y.ToString() + "," + TheUser.homelookat.Z.ToString() + "'}";
					LoginGoodData["sim_ip"]=SimInfo.sim_ip.ToString();
					LoginGoodData["sim_port"]=SimInfo.sim_port;
					LoginGoodData["region_x"]=SimInfo.RegionLocX;
					LoginGoodData["region_y"]=SimInfo.RegionLocY;
					LoginGoodData["ui_config"] = ui_config;
					LoginGoodData["login"]="true";
	
					LoginGoodResp.Value=LoginGoodData;
					return(Regex.Replace(XmlRpcResponseSerializer.Singleton.Serialize(LoginGoodResp),"utf-16","utf-8" ));

				} catch (Exception E) {
					Console.WriteLine(E.ToString());
				}					
					
				break;
			}

			return "";
		}
		
		static string ParseREST(string requestBody, string requestURL) {
			return "";
		}


		static void HandleRequest(Object  stateinfo) {
			HttpListenerContext context=(HttpListenerContext)stateinfo;
		
                	HttpListenerRequest request = context.Request;
                	HttpListenerResponse response = context.Response;

			response.KeepAlive=false;
			response.SendChunked=false;

			System.IO.Stream body = request.InputStream;
			System.Text.Encoding encoding = System.Text.Encoding.UTF8;
			System.IO.StreamReader reader = new System.IO.StreamReader(body, encoding);
   
	    		string requestBody = reader.ReadToEnd();
			body.Close();
    			reader.Close();

                        string responseString="";
			switch(request.ContentType) {
                                case "text/xml":
                                	// must be XML-RPC, so pass to the XML-RPC parser
					
					responseString=ParseXMLRPC(requestBody);
					response.AddHeader("Content-type","text/xml");	
				break;
                        	
				case null:
					// must be REST or invalid crap, so pass to the REST parser
					responseString=ParseREST(request.Url.OriginalString,requestBody);
				break;
			}
	
	
	                byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
        	        System.IO.Stream output = response.OutputStream;
    	        	response.SendChunked=false;
			response.ContentLength64=buffer.Length;
			output.Write(buffer,0,buffer.Length);
        	        output.Close();
		}
	}


}

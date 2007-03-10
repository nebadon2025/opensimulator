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
					
					LLUUID AgentID = TheUser.UUID;
					TheUser.InitSessionData();
					SimProfile SimInfo = new SimProfile();


					XmlRpcResponse LoginGoodResp = new XmlRpcResponse();
					Hashtable LoginGoodData = new Hashtable();
				
					Hashtable GlobalTextures = new Hashtable();
					GlobalTextures["sun_texture_id"] = "cce0f112-878f-4586-a2e2-a8f104bba271";
					GlobalTextures["cloud_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";
					GlobalTextures["moon_texture_id"] = "fc4b9f0b-d008-45c6-96a4-01dd947ac621";

					Hashtable LoginFlags = new Hashtable();
					LoginFlags["daylight_savings"]="N";
					LoginFlags["stipend_since_login"]="N";
					LoginFlags["gendered"]="Y";
					LoginFlags["ever_logged_in"]="Y";

					LoginGoodData["message"]=OpenUser_Main.userserver.DefaultStartupMsg;
					LoginGoodData["session_id"]=TheUser.CurrentSessionID;
					LoginGoodData["secure_sessionid"]=TheUser.CurrentSecureSessionID;
					LoginGoodData["agent_access"]="M";
					LoginGoodData["start_location"]=requestData["start"];
					LoginGoodData["global_textures"]=GlobalTextures;
					LoginGoodData["seconds_since_epoch"]=DateTime.Now;
					LoginGoodData["firstname"]=firstname;
					LoginGoodData["circuit_code"]=(new Random()).Next();
					LoginGoodData["login_flags"]=LoginFlags;
					LoginGoodData["seed_capability"]="http://" + SimInfo.sim_ip + ":12043" + "/cap" + TheUser.CurrentSecureSessionID.Combine(TheUser.CurrentSessionID).Combine(AgentID);

					
					LoginGoodResp.Value=LoginGoodData;
					return(XmlRpcResponseSerializer.Singleton.Serialize(LoginGoodResp));
					
					
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
			System.Text.Encoding encoding = request.ContentEncoding;
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
	
	
	                byte[] buffer = System.Text.Encoding.Unicode.GetBytes(responseString);
        	        System.IO.Stream output = response.OutputStream;
    	        	response.SendChunked=false;
			encoding = System.Text.Encoding.UTF8;
        		response.ContentEncoding = encoding;
			response.ContentLength64=buffer.Length;
			output.Write(buffer,0,buffer.Length);
        	        output.Close();
		}
	}


}

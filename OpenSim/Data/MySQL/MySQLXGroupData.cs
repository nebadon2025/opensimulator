/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Data;
using MySql.Data.MySqlClient;

namespace OpenSim.Data.MySQL
{
    public class MySQLXGroupData : MySQLGenericTableHandler<XGroup>
    {
        //        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public MySQLXGroupData(string connectionString, string realm) : base(connectionString, realm, "XGroups") 
        {
        }

        public bool StoreGroup(XGroup group)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                string sqlGroup;

                if (group.groupID == UUID.Zero)
                {
                    sqlGroup = String.Format("insert into {0} ( GroupID, Name, InsigniaID, FounderID, MembershipFee, OpenEnrollment, ShowInList, AllowPublish,MaturePublish,OwnerRoleID ) " +
                        " values ( ?GroupID, ?Name, ?InsigniaID, ?FounderID, ?MembershipFee, ?OpenEnrollment, ?ShowInList, ?AllowPublish, ?MaturePublish, ?OwnerRoleID ); ",
                               m_Realm);
                    group.groupID = UUID.Random();
                }
                else
                {
                    sqlGroup = String.Format("update {0} set Name = ?Name, InsigniaID = ?InsigniaID, FounderID = ?FounderID, MembershipFee = ?MembershipFee, " +
                        "OpenEnrollment = ?OpenEnrollment, ShowInList = ?ShowInList, AllowPublish = ?AllowPublish, MaturePublish = ?MaturePublish, OwnerRoleID = ?OwnerRoleID " +
                        " where GroupID = ?GroupID;",
                               m_Realm);
                }

                cmd.Parameters.AddWithValue("?groupID",        group.groupID.ToString() );
                cmd.Parameters.AddWithValue("?name",           group.name);
                cmd.Parameters.AddWithValue("?InsigniaID",     group.insigniaID.ToString() );
                cmd.Parameters.AddWithValue("?FounderID",      group.founderID.ToString() );
                cmd.Parameters.AddWithValue("?MembershipFee",  group.membershipFee.ToString() );
                cmd.Parameters.AddWithValue("?OpenEnrollment", ( group.openEnrollment ? '1' : '0' ) );
                cmd.Parameters.AddWithValue("?ShowInList",     ( group.showInList     ? '1' : '0' ) );
                cmd.Parameters.AddWithValue("?AllowPublish",   ( group.allowPublish   ? '1' : '0' ) );
                cmd.Parameters.AddWithValue("?MaturePublish",  ( group.maturePublish  ? '1' : '0' ) );
                cmd.Parameters.AddWithValue("?OwnerRoleID",    group.ownerRoleID.ToString() );

                if (ExecuteNonQuery(cmd) < 1)
                    return false;
            }
            return true;
        }

        public XGroup[] GetGroups(string groupID, string groupName)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
               cmd.CommandText = String.Format("select * from {0} where (GroupID = ?groupID) or (Name like ?search)", m_Realm);
               cmd.Parameters.AddWithValue("?search", "%" + groupName + "%");
               cmd.Parameters.AddWithValue("?groupID", groupID);

               return DoQuery(cmd);
            }
        }

        public bool DeleteGroups(string[] fields, string[] vals)
        {
            return false;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Data.DB4o
{
    public class DB4oUserData : IUserData
    {
        public UserProfileData getUserByUUID(LLUUID uuid)
        {
            return new UserProfileData();
        }

        public UserProfileData getUserByName(string name)
        {
            return getUserByName(name.Split(',')[0], name.Split(',')[1]);
        }

        public UserProfileData getUserByName(string fname, string lname)
        {
            return new UserProfileData();
        }

        public UserAgentData getAgentByUUID(LLUUID uuid)
        {
            return new UserAgentData();
        }

        public UserAgentData getAgentByName(string name)
        {
            return getAgentByName(name.Split(',')[0], name.Split(',')[1]);
        }

        public UserAgentData getAgentByName(string fname, string lname)
        {
            return new UserAgentData();
        }

        public bool moneyTransferRequest(LLUUID from, LLUUID to, uint amount)
        {
            return true;
        }

        public bool inventoryTransferRequest(LLUUID from, LLUUID to, LLUUID item)
        {
            return true;
        }

    }
}

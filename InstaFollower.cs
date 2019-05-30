using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGTomesheqAutoLiker
{
    enum FollowerState { Unfollow = 0, KeepFollowing = 1, Admin = 2 }

    class InstaFollower
    {
        public InitDataHolder data_holder;
        public Image profile_pic;
        public string user_name;
        public string panel_name;
        public string label_name;
        public long user_id;
        public FollowerState follower_state;

        // konstruktor uzywany, gdy user nie istnieje w bazie danych
        public InstaFollower(InitDataHolder holder, Image _profile_pic, string _user_name, long _userId)
        {
            user_name = _user_name;
            user_id = _userId;
            data_holder = holder;
            profile_pic = _profile_pic;
            panel_name = "panel_";
            label_name = "label_";
            follower_state = FollowerState.KeepFollowing;
        }

        // konstruktor uzywany, gdy user istnieje w bazie danych
        public InstaFollower(InitDataHolder holder, Image _profile_pic, string _user_name, long _userId, FollowerState _follower_state)
        {
            user_name = _user_name;
            user_id = _userId;
            data_holder = holder;
            profile_pic = _profile_pic;
            panel_name = "panel_";
            label_name = "label_";
            follower_state = _follower_state;
        }

        public void Follow()
        {
            // zmien status w programie
            follower_state = FollowerState.KeepFollowing;

            // zapisz zmiany w bazie danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE instagram_followers SET status = '1' WHERE user_name = '{user_name}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }
        }

        public void Unfollow()
        {
            // zmien status w programie
            follower_state = FollowerState.Unfollow;

            // zapisz zmiany w bazie danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE instagram_followers SET status = '0' WHERE user_name = '{user_name}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }
        }

        public void MarkAsAdmin()
        {
            // zmien status w programie
            follower_state = FollowerState.Admin;

            // zapisz zmiany w bazie danych
            using (SQLiteConnection m_dbConnection = new SQLiteConnection(data_holder.connectionString))
            {
                m_dbConnection.Open();
                string sql = $"UPDATE instagram_followers SET status = '2' WHERE user_name = '{user_name}'";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery(); // nic nie zwraca
                m_dbConnection.Close();
            }
        }

        public void SetPanelNr(int nummer)
        {
            if (nummer != -1)
            {
                panel_name = "panel_" + nummer.ToString();
                label_name = "label_" + nummer.ToString(); 
            }
            else
            {
                panel_name = "panel_";
                label_name = "label_";
            }
        }
    }
}

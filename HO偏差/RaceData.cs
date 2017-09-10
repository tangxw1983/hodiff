using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;

namespace HO偏差
{
    [Serializable()]
    class RaceDataItem
    {
        public RaceDataItem()
        {
            _waters = new WaterTable();
            _odds = new HrsTable();
        }

        private WaterTable _waters;
        private HrsTable _odds;

        public WaterTable Waters
        {
            get
            {
                return _waters;
            }
        }

        public HrsTable Odds
        {
            get
            {
                return _odds;
            }
        }
    }

    [Serializable()]
    class RaceData : Dictionary<long, RaceDataItem>
    {
        public RaceData()
        {

        }

        protected RaceData(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            CardID = info.GetUInt64("RaceData_CardID");
            RaceNo = info.GetInt32("RaceData_RaceNo");
            StartTime = info.GetDateTime("RaceData_StartTime");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("RaceData_CardID", CardID);
            info.AddValue("RaceData_RaceNo", RaceNo);
            info.AddValue("RaceData_StartTime", StartTime);
        }

        public ulong CardID { get; set; }
        public int RaceNo { get; set; }
        public DateTime StartTime { get; set; }

        public RaceDataItem GetNearestItem(long time, long max_distance)
        {
            IEnumerable<long> list = this.Keys.Where(x => Math.Abs(x - time) < max_distance);
            if (list.Count() > 0)
                return this[list.OrderBy(x => Math.Abs(x - time)).First()];
            else
                return null;
        }

        public long MinTime
        {
            get
            {
                return this.Keys.OrderBy(x => x).First();
            }
        }

        public long MaxTime
        {
            get
            {
                return this.Keys.OrderBy(x => x).Last();
            }
        }


        public void Save(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(fs, this);
            }
        }

        public static RaceData Load(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                BinaryFormatter bf = new BinaryFormatter();
                return (RaceData)bf.Deserialize(fs);
            }
        }
    }
}

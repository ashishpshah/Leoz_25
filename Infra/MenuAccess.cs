using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Leoz_25.Infra
{
    public class MenuAccess
    {
        public long Id { get; set; }
        public long ParentId { get; set; }
        public string Area { get; set; }
        public string Controller { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public int DisplayOrder { get; set; }
    }
}
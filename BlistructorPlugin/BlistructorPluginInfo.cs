using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace BlistructorPlugin
{
    public class BlistructorPluginInfo : GH_AssemblyInfo
    {
        public override string Name
        {
            get
            {
                return "Blistructor";
            }
        }
        public override Bitmap Icon
        {
            get
            {
                //Return a 24x24 pixel bitmap to represent this GHA library.
                return null;
            }
        }
        public override string Description
        {
            get
            {
                //Return a short string describing the purpose of this GHA library.
                return "";
            }
        }
        public override Guid Id
        {
            get
            {
                return new Guid("c18d214f-5c2f-4a32-b078-5fcbbdcc78a8");
            }
        }

        public override string AuthorName
        {
            get
            {
                //Return a string identifying you or your company.
                return "";
            }
        }
        public override string AuthorContact
        {
            get
            {
                //Return a string representing your preferred contact details.
                return "";
            }
        }
    }
}

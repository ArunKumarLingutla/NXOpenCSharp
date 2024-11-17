using NXOpen;
using NXOpenUI;
using NXOpen.UF;

namespace NXOpenCS
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Source source = new Source();
            //Source.GetBodiesFacesEdges();
            //Source.GetAllComponents();
            source.CreateLine();
            source.CreatePointPerpendicularToLine();
            //source.CreateLinePerpendicularToExistingLine();
        }
        public static int GetUnloadOption(string dummy)
        {
            return (int)Session.LibraryUnloadOption.Immediately;
        }
    }
}

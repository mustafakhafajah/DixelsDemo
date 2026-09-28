using Volo.Abp.Application.Dtos;

namespace Dixels.Portal.Estate;

/* List input for buildings, floors, spaces and space types. The screens show the whole list, so when the
 * client sends no paging it gets every row (up to ABP's cap) instead of ABP's default of 10. */
public class EstateListInput : PagedAndSortedResultRequestDto
{
    public EstateListInput()
    {
        MaxResultCount = MaxMaxResultCount;
    }
}

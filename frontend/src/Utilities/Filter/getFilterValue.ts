import { Filter, FilterExpression, PropertyFilter } from 'App/State/AppState';

function isPropertyFilter(filter: FilterExpression): filter is PropertyFilter {
  return 'key' in filter;
}

export default function getFilterValue<T>(
  filters: Filter[],
  filterKey: string | number,
  filterValueKey: string,
  defaultValue: T
) {
  const filter = filters.find((f) => f.key === filterKey);

  if (!filter) {
    return defaultValue;
  }

  const filterValue = filter.filters.find((f): f is PropertyFilter => {
    return isPropertyFilter(f) && f.key === filterValueKey;
  });

  return filterValue ? filterValue.value : defaultValue;
}

export const FILTER_GROUP_KIND = 'group';
export const FILTER_COMBINATOR_AND = 'and';
export const FILTER_COMBINATOR_OR = 'or';

export function isFilterGroup(filter) {
  return !!filter &&
    filter.kind === FILTER_GROUP_KIND &&
    Array.isArray(filter.filters);
}

export function createFilterGroup(combinator = FILTER_COMBINATOR_AND, filters = []) {
  return {
    kind: FILTER_GROUP_KIND,
    combinator,
    filters
  };
}

export function normalizeFilterGroup(filters) {
  if (isFilterGroup(filters)) {
    return filters;
  }

  if (Array.isArray(filters) && filters.length === 1 && isFilterGroup(filters[0])) {
    return filters[0];
  }

  return createFilterGroup(FILTER_COMBINATOR_AND, Array.isArray(filters) ? filters : []);
}

export function serializeFilterGroup(group) {
  const filters = group.filters || [];

  if (
    group.combinator === FILTER_COMBINATOR_AND &&
    filters.every((filter) => !isFilterGroup(filter))
  ) {
    return filters;
  }

  return [group];
}

export function hasFilterGroups(filters) {
  if (isFilterGroup(filters)) {
    return true;
  }

  if (!Array.isArray(filters)) {
    return false;
  }

  return filters.some((filter) => {
    return isFilterGroup(filter) || hasFilterGroups(filter.filters);
  });
}

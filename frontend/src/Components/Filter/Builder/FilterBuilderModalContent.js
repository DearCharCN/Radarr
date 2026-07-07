import { maxBy } from 'lodash';
import PropTypes from 'prop-types';
import React, { Component } from 'react';
import FormInputGroup from 'Components/Form/FormInputGroup';
import Button from 'Components/Link/Button';
import SpinnerErrorButton from 'Components/Link/SpinnerErrorButton';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { inputTypes } from 'Helpers/Props';
import { createFilterGroup, normalizeFilterGroup, serializeFilterGroup } from 'Utilities/Filter/filterTree';
import translate from 'Utilities/String/translate';
import FilterBuilderGroup from './FilterBuilderGroup';
import styles from './FilterBuilderModalContent.css';

function ensureGroupHasFilter(group) {
  if (group.filters.length) {
    return group;
  }

  return {
    ...group,
    filters: [{}]
  };
}

function updateNodeAtPath(group, path, updater) {
  if (!path.length) {
    return updater(group);
  }

  const [index, ...childPath] = path;
  const filters = [...group.filters];
  const child = filters[index];

  filters[index] = childPath.length ?
    updateNodeAtPath(child, childPath, updater) :
    updater(child);

  return {
    ...group,
    filters
  };
}

function addChildToGroup(group, path, child) {
  return updateNodeAtPath(group, path, (node) => {
    return {
      ...node,
      filters: [
        ...node.filters,
        child
      ]
    };
  });
}

function insertNodeAfterPath(group, path, node) {
  const parentPath = path.slice(0, -1);
  const index = path[path.length - 1];

  return updateNodeAtPath(group, parentPath, (parent) => {
    const filters = [...parent.filters];
    filters.splice(index + 1, 0, node);

    return {
      ...parent,
      filters
    };
  });
}

function removeNodeAtPath(group, path) {
  const parentPath = path.slice(0, -1);
  const index = path[path.length - 1];

  return updateNodeAtPath(group, parentPath, (parent) => {
    if (parent.filters.length === 1) {
      return parent;
    }

    const filters = [...parent.filters];
    filters.splice(index, 1);

    return {
      ...parent,
      filters
    };
  });
}

class FilterBuilderModalContent extends Component {

  //
  // Lifecycle

  constructor(props, context) {
    super(props, context);

    const filterGroup = ensureGroupHasFilter(normalizeFilterGroup(props.filters));

    this.state = {
      label: props.label,
      filterGroup,
      labelErrors: []
    };
  }

  componentDidUpdate(prevProps) {
    const {
      id,
      customFilters,
      isSaving,
      saveError,
      dispatchSetFilter,
      onModalClose
    } = this.props;

    if (prevProps.isSaving && !isSaving && !saveError) {
      if (id) {
        dispatchSetFilter({ selectedFilterKey: id });
      } else {
        const last = maxBy(customFilters, 'id');
        dispatchSetFilter({ selectedFilterKey: last.id });
      }

      onModalClose();
    }
  }

  //
  // Listeners

  onLabelChange = ({ value }) => {
    this.setState({ label: value });
  };

  onGroupChange = (path, group) => {
    this.setState({
      filterGroup: updateNodeAtPath(this.state.filterGroup, path, () => group)
    });
  };

  onFilterChange = (path, filter) => {
    this.setState({
      filterGroup: updateNodeAtPath(this.state.filterGroup, path, () => filter)
    });
  };

  onAddFilterPress = (path) => {
    const filterGroup = path.length ?
      insertNodeAfterPath(this.state.filterGroup, path, {}) :
      addChildToGroup(this.state.filterGroup, path, {});

    this.setState({
      filterGroup
    });
  };

  onAddGroupPress = (path) => {
    const group = createFilterGroup('and', [{}]);
    const filterGroup = path.length ?
      insertNodeAfterPath(this.state.filterGroup, path, group) :
      addChildToGroup(this.state.filterGroup, path, group);

    this.setState({
      filterGroup
    });
  };

  onRemovePress = (path) => {
    this.setState({
      filterGroup: removeNodeAtPath(this.state.filterGroup, path)
    });
  };

  onSaveFilterPress = () => {
    const {
      id,
      customFilterType,
      onSaveCustomFilterPress
    } = this.props;

    const {
      label,
      filterGroup
    } = this.state;

    if (!label) {
      this.setState({
        labelErrors: [
          {
            message: translate('LabelIsRequired')
          }
        ]
      });

      return;
    }

    onSaveCustomFilterPress({
      id,
      type: customFilterType,
      label,
      filters: serializeFilterGroup(filterGroup)
    });
  };

  //
  // Render

  render() {
    const {
      sectionItems,
      filterBuilderProps,
      isSaving,
      saveError,
      onCancelPress,
      onModalClose
    } = this.props;

    const {
      label,
      filterGroup,
      labelErrors
    } = this.state;

    return (
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>
          {translate('CustomFilter')}
        </ModalHeader>

        <ModalBody>
          <div className={styles.labelContainer}>
            <div className={styles.label}>
              {translate('Label')}
            </div>

            <div className={styles.labelInputContainer}>
              <FormInputGroup
                name="label"
                value={label}
                type={inputTypes.TEXT}
                errors={labelErrors}
                onChange={this.onLabelChange}
              />
            </div>
          </div>

          <div className={styles.label}>{translate('Filters')}</div>

          <div className={styles.rows}>
            <FilterBuilderGroup
              group={filterGroup}
              path={[]}
              isRoot={true}
              filterCount={1}
              sectionItems={sectionItems}
              filterBuilderProps={filterBuilderProps}
              onGroupChange={this.onGroupChange}
              onFilterChange={this.onFilterChange}
              onAddFilterPress={this.onAddFilterPress}
              onAddGroupPress={this.onAddGroupPress}
              onRemovePress={this.onRemovePress}
            />
          </div>
        </ModalBody>

        <ModalFooter>
          <Button onPress={onCancelPress}>
            {translate('Cancel')}
          </Button>

          <SpinnerErrorButton
            isSpinning={isSaving}
            error={saveError}
            onPress={this.onSaveFilterPress}
          >
            {translate('Save')}
          </SpinnerErrorButton>
        </ModalFooter>
      </ModalContent>
    );
  }
}

FilterBuilderModalContent.propTypes = {
  id: PropTypes.number,
  label: PropTypes.string.isRequired,
  customFilterType: PropTypes.string.isRequired,
  sectionItems: PropTypes.arrayOf(PropTypes.object).isRequired,
  filters: PropTypes.arrayOf(PropTypes.object).isRequired,
  filterBuilderProps: PropTypes.arrayOf(PropTypes.object).isRequired,
  customFilters: PropTypes.arrayOf(PropTypes.object).isRequired,
  isSaving: PropTypes.bool.isRequired,
  saveError: PropTypes.object,
  dispatchDeleteCustomFilter: PropTypes.func.isRequired,
  onSaveCustomFilterPress: PropTypes.func.isRequired,
  dispatchSetFilter: PropTypes.func.isRequired,
  onCancelPress: PropTypes.func.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default FilterBuilderModalContent;

/* eslint-disable react/prop-types */
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Card from 'Components/Card';
import FieldSet from 'Components/FieldSet';
import SelectInput from 'Components/Form/SelectInput';
import Icon from 'Components/Icon';
import Label from 'Components/Label';
import Button from 'Components/Link/Button';
import IconButton from 'Components/Link/IconButton';
import SpinnerButton from 'Components/Link/SpinnerButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { icons, kinds } from 'Helpers/Props';
import SettingsToolbar from 'Settings/SettingsToolbar';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './ReleaseFilterSettingsPage.css';

const ENDPOINT = '/releasefilterprofile';

const DEFAULT_FILTER = {
  type: 'group',
  mode: 'and',
  children: []
};

const FIELD_OPTIONS = [
  { key: 'title', label: 'Title', valueType: 'string' },
  { key: 'indexer', label: 'Indexer', valueType: 'string' },
  { key: 'protocol', label: 'Protocol', valueType: 'string' },
  { key: 'quality', label: 'Quality', valueType: 'string' },
  { key: 'qualityId', label: 'QualityId', valueType: 'number' },
  { key: 'customFormats', label: 'CustomFormats', valueType: 'string' },
  { key: 'customFormatScore', label: 'CustomFormatScore', valueType: 'number' },
  { key: 'size', label: 'Size', valueType: 'number' },
  { key: 'age', label: 'Age', valueType: 'number' },
  { key: 'ageHours', label: 'AgeHours', valueType: 'number' },
  { key: 'ageMinutes', label: 'AgeMinutes', valueType: 'number' },
  { key: 'seeders', label: 'Seeders', valueType: 'number' },
  { key: 'peers', label: 'Peers', valueType: 'number' },
  { key: 'leechers', label: 'Leechers', valueType: 'number' },
  { key: 'releaseGroup', label: 'ReleaseGroup', valueType: 'string' },
  { key: 'languages', label: 'Languages', valueType: 'string' },
  { key: 'audioLanguages', label: 'AudioLanguages', valueType: 'string' },
  { key: 'audioLanguageTags', label: 'AudioLanguageTags', valueType: 'string' },
  { key: 'audioInfo', label: 'AudioInfo', valueType: 'string' },
  { key: 'audioSpecifications', label: 'AudioSpecifications', valueType: 'string' },
  { key: 'subtitleLanguages', label: 'SubtitleLanguages', valueType: 'string' },
  { key: 'selectedAudio', label: 'SelectedAudio', valueType: 'string' },
  { key: 'selectedAudioLanguage', label: 'SelectedAudioLanguage', valueType: 'string' },
  { key: 'selectedAudioSpecification', label: 'SelectedAudioSpecification', valueType: 'string' },
  { key: 'selectedAudioTags', label: 'SelectedAudioTags', valueType: 'string' },
  { key: 'audioScore', label: 'AudioScore', valueType: 'number' },
  { key: 'hasChineseAudioOrSubtitle', label: 'HasChineseAudioOrSubtitle', valueType: 'boolean' },
  { key: 'hasOriginAudio', label: 'HasOriginAudio', valueType: 'boolean' },
  { key: 'mediaInfoStatus', label: 'MediaInfoStatus', valueType: 'string' }
];

const STRING_OPERATORS = [
  { key: 'contains', label: 'FilterContains' },
  { key: 'notContains', label: 'FilterDoesNotContain' },
  { key: 'equal', label: 'FilterEqual' },
  { key: 'notEqual', label: 'FilterNotEqual' },
  { key: 'exists', label: 'FilterExists' },
  { key: 'notExists', label: 'FilterDoesNotExist' }
];

const NUMBER_OPERATORS = [
  { key: 'equal', label: 'FilterEqual' },
  { key: 'notEqual', label: 'FilterNotEqual' },
  { key: 'greaterThan', label: 'FilterGreaterThan' },
  { key: 'greaterThanOrEqual', label: 'FilterGreaterThanOrEqual' },
  { key: 'lessThan', label: 'FilterLessThan' },
  { key: 'lessThanOrEqual', label: 'FilterLessThanOrEqual' },
  { key: 'exists', label: 'FilterExists' },
  { key: 'notExists', label: 'FilterDoesNotExist' }
];

const BOOLEAN_OPERATORS = [
  { key: 'equal', label: 'FilterIs' },
  { key: 'notEqual', label: 'FilterIsNot' },
  { key: 'exists', label: 'FilterExists' },
  { key: 'notExists', label: 'FilterDoesNotExist' }
];

const VALUELESS_OPERATORS = new Set(['exists', 'notExists']);

const GROUP_MODE_OPTIONS = [
  {
    key: 'and',
    value: 'and'
  },
  {
    key: 'or',
    value: 'or'
  }
];

function requestJson({ url, method = 'GET', data }) {
  const ajaxOptions = {
    url,
    method
  };

  if (method !== 'DELETE') {
    ajaxOptions.dataType = 'json';
  }

  if (data !== undefined) {
    ajaxOptions.contentType = 'application/json';
    ajaxOptions.data = JSON.stringify(data);
  }

  return new Promise((resolve, reject) => {
    const { request } = createAjaxRequest(ajaxOptions);

    request.done((result) => resolve(result));
    request.fail((xhr) => reject(xhr));
  });
}

function getErrorMessage(xhr, fallback) {
  const response = xhr?.responseJSON;

  if (Array.isArray(response)) {
    return response
      .map((item) => item.errorMessage || item.message || item.propertyName)
      .filter(Boolean)
      .join(' ');
  }

  if (response?.errors) {
    return Object.values(response.errors).flat().join(' ');
  }

  return response?.message || response?.error || xhr?.statusText || fallback;
}

function cloneFilterNode(node) {
  if (!node) {
    return { ...DEFAULT_FILTER, children: [] };
  }

  return {
    ...node,
    children: (node.children || []).map(cloneFilterNode)
  };
}

function createDefaultProfile() {
  return {
    id: 0,
    name: translate('DefaultReleaseFilterProfileName'),
    enabled: true,
    filter: cloneFilterNode(DEFAULT_FILTER)
  };
}

function cloneProfile(profile) {
  return {
    ...profile,
    filter: cloneFilterNode(profile.filter)
  };
}

function createConditionNode() {
  return {
    type: 'condition',
    field: FIELD_OPTIONS[0].key,
    operator: 'contains',
    value: ''
  };
}

function createGroupNode(children = []) {
  return {
    type: 'group',
    mode: 'and',
    children
  };
}

function getFieldOption(field) {
  return FIELD_OPTIONS.find((option) => option.key === field) || FIELD_OPTIONS[0];
}

function getOperatorOptions(field) {
  const valueType = getFieldOption(field).valueType;

  if (valueType === 'number') {
    return NUMBER_OPERATORS;
  }

  if (valueType === 'boolean') {
    return BOOLEAN_OPERATORS;
  }

  return STRING_OPERATORS;
}

function normalizeCondition(node) {
  const field = node.field || FIELD_OPTIONS[0].key;
  const operatorOptions = getOperatorOptions(field);
  const operator = operatorOptions.some((option) => option.key === node.operator) ?
    node.operator :
    operatorOptions[0].key;

  return {
    ...node,
    type: 'condition',
    field,
    operator
  };
}

function countConditions(node) {
  if (!node) {
    return 0;
  }

  if (node.type !== 'group') {
    return 1;
  }

  return (node.children || []).reduce((total, child) => total + countConditions(child), 0);
}

function updateNodeAtPath(node, path, updater) {
  if (!path.length) {
    return updater(node);
  }

  const [index, ...rest] = path;

  return {
    ...node,
    children: (node.children || []).map((child, childIndex) => {
      return childIndex === index ? updateNodeAtPath(child, rest, updater) : child;
    })
  };
}

function addChildToGroup(node, path, child) {
  return updateNodeAtPath(node, path, (group) => ({
    ...group,
    type: 'group',
    children: [
      ...(group.children || []),
      child
    ]
  }));
}

function insertNodeAfterPath(node, path, child) {
  if (!path.length) {
    return addChildToGroup(node, path, child);
  }

  const parentPath = path.slice(0, -1);
  const insertIndex = path[path.length - 1];

  return updateNodeAtPath(node, parentPath, (parent) => {
    const children = [...(parent.children || [])];
    children.splice(insertIndex + 1, 0, child);

    return {
      ...parent,
      children
    };
  });
}

function removeNodeAtPath(node, path) {
  if (!path.length) {
    return node;
  }

  const parentPath = path.slice(0, -1);
  const removeIndex = path[path.length - 1];

  return updateNodeAtPath(node, parentPath, (parent) => ({
    ...parent,
    children: (parent.children || []).filter((_, index) => index !== removeIndex)
  }));
}

function AddCard({ onPress }) {
  return (
    <Card
      className={styles.addCard}
      onPress={onPress}
    >
      <div className={styles.center}>
        <Icon
          name={icons.ADD}
          size={45}
        />
      </div>
    </Card>
  );
}

function ActionButtons({ onEditPress, onDeletePress }) {
  return (
    <div className={styles.actions}>
      <IconButton
        aria-label={translate('Edit')}
        title={translate('Edit')}
        name={icons.EDIT}
        size={14}
        onPress={onEditPress}
      />

      <IconButton
        aria-label={translate('Delete')}
        title={translate('Delete')}
        name={icons.DELETE}
        size={14}
        onPress={onDeletePress}
      />
    </div>
  );
}

function ProfileCard({ profile, onEditPress, onDeletePress }) {
  const conditionCount = countConditions(profile.filter);
  const mode = profile.filter?.mode === 'or' ? translate('MatchAny') : translate('MatchAll');

  return (
    <Card
      className={styles.configCard}
      overlayContent={true}
      onPress={onEditPress}
    >
      <div className={styles.cardTitleContainer}>
        <div className={styles.cardTitle}>{profile.name}</div>

        <ActionButtons
          onEditPress={onEditPress}
          onDeletePress={onDeletePress}
        />
      </div>

      <div className={styles.labels}>
        <Label className={styles.label}>
          {mode}
        </Label>

        <Label className={styles.label}>
          {conditionCount} {translate('FilterConditions')}
        </Label>

        {profile.enabled ? null : (
          <Label className={styles.label} kind={kinds.WARNING}>
            {translate('Disabled')}
          </Label>
        )}
      </div>
    </Card>
  );
}

function TextField({ label, value, onChange }) {
  return (
    <label className={styles.field}>
      <span>{label}</span>
      <input
        className={styles.input}
        type="text"
        value={value || ''}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function CheckboxField({ label, checked, onChange }) {
  return (
    <label className={styles.checkboxField}>
      <input
        type="checkbox"
        checked={!!checked}
        onChange={(event) => onChange(event.target.checked)}
      />
      <span>{label}</span>
    </label>
  );
}

function FilterValueInput({ node, onChange }) {
  const field = getFieldOption(node.field);

  if (VALUELESS_OPERATORS.has(node.operator)) {
    return (
      <div className={styles.compactValuePlaceholder}>
        {translate('NoValueRequired')}
      </div>
    );
  }

  if (field.valueType === 'boolean') {
    return (
      <select
        className={styles.compactInput}
        value={node.value === false || node.value === 'false' ? 'false' : 'true'}
        onChange={(event) => onChange(event.target.value === 'true')}
      >
        <option value="true">{translate('Yes')}</option>
        <option value="false">{translate('No')}</option>
      </select>
    );
  }

  if (field.valueType === 'number') {
    return (
      <input
        className={styles.compactInput}
        type="number"
        value={node.value ?? 0}
        onChange={(event) => {
          const value = Number.parseFloat(event.target.value);
          onChange(Number.isNaN(value) ? 0 : value);
        }}
      />
    );
  }

  return (
    <input
      className={styles.compactInput}
      type="text"
      value={node.value}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}

function FilterConditionEditor({
  node,
  path,
  onNodeChange,
  onNodeRemove,
  onAddFilterAfterPress
}) {
  const normalizedNode = normalizeCondition(node);
  const operatorOptions = getOperatorOptions(normalizedNode.field);
  const fieldOptions = FIELD_OPTIONS.map((field) => ({
    key: field.key,
    value: translate(field.label)
  }));
  const operatorSelectOptions = operatorOptions.map((operator) => ({
    key: operator.key,
    value: translate(operator.label)
  }));

  return (
    <div className={styles.filterRow}>
      <div className={styles.inputContainer}>
        <SelectInput
          name="field"
          value={normalizedNode.field}
          values={fieldOptions}
          onChange={({ value: field }) => {
            const nextOperators = getOperatorOptions(field);
            const nextOperator = nextOperators.some((option) => option.key === normalizedNode.operator) ?
              normalizedNode.operator :
              nextOperators[0].key;

            onNodeChange(path, {
              ...normalizedNode,
              field,
              operator: nextOperator
            });
          }}
        />
      </div>

      <div className={styles.inputContainer}>
        <SelectInput
          name="operator"
          value={normalizedNode.operator}
          values={operatorSelectOptions}
          onChange={({ value: operator }) => {
            onNodeChange(path, {
              ...normalizedNode,
              operator
            });
          }}
        />
      </div>

      <div className={styles.valueInputContainer}>
        <FilterValueInput
          node={normalizedNode}
          onChange={(value) => {
            onNodeChange(path, {
              ...normalizedNode,
              value
            });
          }}
        />
      </div>

      <div className={styles.actionsContainer}>
        <IconButton
          title={translate('Delete')}
          name={icons.SUBTRACT}
          onPress={() => onNodeRemove(path)}
        />

        <IconButton
          title={translate('AddFilterCondition')}
          name={icons.ADD}
          onPress={() => onAddFilterAfterPress(path)}
        />
      </div>
    </div>
  );
}

function FilterGroupEditor({
  node,
  path,
  isRoot,
  onNodeChange,
  onNodeRemove,
  onAddFilterToGroupPress,
  onAddGroupToGroupPress,
  onAddFilterAfterPress
}) {
  const children = node.children || [];

  return (
    <div className={isRoot ? styles.rootFilterGroup : styles.filterGroup}>
      <div className={styles.filterGroupHeader}>
        <div className={styles.combinatorContainer}>
          <SelectInput
            name="mode"
            value={node.mode || 'and'}
            values={GROUP_MODE_OPTIONS}
            onChange={({ value: mode }) => {
              onNodeChange(path, {
                ...node,
                type: 'group',
                mode
              });
            }}
          />
        </div>

        <div className={styles.groupActionsContainer}>
          <IconButton
            title={translate('AddFilterCondition')}
            name={icons.ADD}
            onPress={() => onAddFilterToGroupPress(path)}
          />

          <IconButton
            title={translate('AddFilterGroup')}
            name={icons.GROUP}
            onPress={() => onAddGroupToGroupPress(path)}
          />

          {isRoot ? null : (
            <IconButton
              title={translate('Delete')}
              name={icons.SUBTRACT}
              onPress={() => onNodeRemove(path)}
            />
          )}
        </div>
      </div>

      <div className={styles.filterGroupChildren}>
        {children.length ? children.map((child, index) => (
          <FilterNodeEditor
            key={`${child.type || 'condition'}-${path.concat(index).join('.')}`}
            node={child}
            path={[...path, index]}
            onNodeChange={onNodeChange}
            onNodeRemove={onNodeRemove}
            onAddFilterToGroupPress={onAddFilterToGroupPress}
            onAddGroupToGroupPress={onAddGroupToGroupPress}
            onAddFilterAfterPress={onAddFilterAfterPress}
          />
        )) : (
          <div className={styles.emptyFilterGroup}>
            {translate('NoFilterConditions')}
          </div>
        )}
      </div>
    </div>
  );
}

function FilterNodeEditor({
  node,
  path,
  onNodeChange,
  onNodeRemove,
  onAddFilterToGroupPress,
  onAddGroupToGroupPress,
  onAddFilterAfterPress
}) {
  if (node.type === 'group') {
    return (
      <FilterGroupEditor
        node={node}
        path={path}
        isRoot={path.length === 0}
        onNodeChange={onNodeChange}
        onNodeRemove={onNodeRemove}
        onAddFilterToGroupPress={onAddFilterToGroupPress}
        onAddGroupToGroupPress={onAddGroupToGroupPress}
        onAddFilterAfterPress={onAddFilterAfterPress}
      />
    );
  }

  return (
    <FilterConditionEditor
      node={node}
      path={path}
      onNodeChange={onNodeChange}
      onNodeRemove={onNodeRemove}
      onAddFilterAfterPress={onAddFilterAfterPress}
    />
  );
}

function EditProfileModal({
  draft,
  isSaving,
  saveError,
  setDraft,
  onSavePress,
  onModalClose
}) {
  const title = draft?.id ? translate('EditReleaseFilterProfile') : translate('AddReleaseFilterProfile');

  const updateFilterNode = useCallback((path, nextNode) => {
    setDraft((current) => ({
      ...current,
      filter: updateNodeAtPath(current.filter || cloneFilterNode(DEFAULT_FILTER), path, () => nextNode)
    }));
  }, [setDraft]);

  const removeFilterNode = useCallback((path) => {
    setDraft((current) => ({
      ...current,
      filter: removeNodeAtPath(current.filter || cloneFilterNode(DEFAULT_FILTER), path)
    }));
  }, [setDraft]);

  const addFilterToGroup = useCallback((path) => {
    setDraft((current) => ({
      ...current,
      filter: addChildToGroup(
        current.filter || cloneFilterNode(DEFAULT_FILTER),
        path,
        createConditionNode()
      )
    }));
  }, [setDraft]);

  const addGroupToGroup = useCallback((path) => {
    setDraft((current) => ({
      ...current,
      filter: addChildToGroup(
        current.filter || cloneFilterNode(DEFAULT_FILTER),
        path,
        createGroupNode([createConditionNode()])
      )
    }));
  }, [setDraft]);

  const addFilterAfter = useCallback((path) => {
    setDraft((current) => ({
      ...current,
      filter: insertNodeAfterPath(
        current.filter || cloneFilterNode(DEFAULT_FILTER),
        path,
        createConditionNode()
      )
    }));
  }, [setDraft]);

  if (!draft) {
    return null;
  }

  return (
    <Modal isOpen={true} size="large"
      onModalClose={onModalClose}
    >
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{title}</ModalHeader>

        <ModalBody>
          {saveError ? <Alert kind="danger">{saveError}</Alert> : null}

          <div className={styles.formGrid}>
            <TextField
              label={translate('Name')}
              value={draft.name}
              onChange={(value) => {
                setDraft((current) => ({
                  ...current,
                  name: value
                }));
              }}
            />
          </div>

          <CheckboxField
            label={translate('Enabled')}
            checked={draft.enabled}
            onChange={(value) => {
              setDraft((current) => ({
                ...current,
                enabled: value
              }));
            }}
          />

          <div className={styles.filterSectionLabel}>{translate('Filters')}</div>

          <div className={styles.filterRows}>
            <FilterNodeEditor
              node={draft.filter || cloneFilterNode(DEFAULT_FILTER)}
              path={[]}
              onNodeChange={updateFilterNode}
              onNodeRemove={removeFilterNode}
              onAddFilterToGroupPress={addFilterToGroup}
              onAddGroupToGroupPress={addGroupToGroup}
              onAddFilterAfterPress={addFilterAfter}
            />
          </div>
        </ModalBody>

        <ModalFooter>
          <Button onPress={onModalClose}>{translate('Cancel')}</Button>

          <SpinnerButton
            kind="primary"
            isSpinning={isSaving}
            onPress={onSavePress}
          >
            {translate('Save')}
          </SpinnerButton>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

function ReleaseFilterSettingsPage() {
  const [items, setItems] = useState([]);
  const [isFetching, setIsFetching] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [draft, setDraft] = useState(null);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const sortedItems = useMemo(() => {
    return [...items].sort((a, b) => a.name.localeCompare(b.name));
  }, [items]);

  const loadData = useCallback(async() => {
    setIsFetching(true);
    setLoadError(null);

    try {
      const profiles = await requestJson({ url: ENDPOINT });
      setItems(profiles || []);
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('ReleaseFilterProfilesLoadError')));
    } finally {
      setIsFetching(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  function openAddModal() {
    setSaveError(null);
    setDraft(createDefaultProfile());
  }

  function openEditModal(item) {
    setSaveError(null);
    setDraft(cloneProfile(item));
  }

  const closeModal = useCallback(() => {
    if (isSaving) {
      return;
    }

    setDraft(null);
    setSaveError(null);
  }, [isSaving]);

  const saveDraft = useCallback(async() => {
    if (!draft) {
      return;
    }

    setIsSaving(true);
    setSaveError(null);

    try {
      const id = draft.id;
      const method = id ? 'PUT' : 'POST';
      const url = id ? `${ENDPOINT}/${id}` : ENDPOINT;

      await requestJson({
        url,
        method,
        data: {
          ...draft,
          filter: draft.filter || cloneFilterNode(DEFAULT_FILTER)
        }
      });

      setDraft(null);
      await loadData();
    } catch (error) {
      setSaveError(getErrorMessage(error, translate('ReleaseFilterProfilesSaveError')));
    } finally {
      setIsSaving(false);
    }
  }, [draft, loadData]);

  const confirmDelete = useCallback(async() => {
    if (!deleteTarget) {
      return;
    }

    setIsDeleting(true);

    try {
      await requestJson({
        url: `${ENDPOINT}/${deleteTarget.id}`,
        method: 'DELETE'
      });

      setDeleteTarget(null);
      await loadData();
    } catch (error) {
      setLoadError(getErrorMessage(error, translate('ReleaseFilterProfilesDeleteError')));
    } finally {
      setIsDeleting(false);
    }
  }, [deleteTarget, loadData]);

  const cancelDelete = useCallback(() => {
    setDeleteTarget(null);
  }, []);

  return (
    <PageContent title={translate('FiltersSettings')}>
      <SettingsToolbar showSave={false} hasPendingChanges={false} />

      <PageContentBody>
        {loadError ? <Alert kind="danger">{loadError}</Alert> : null}

        {isFetching ? (
          <LoadingIndicator />
        ) : (
          <FieldSet legend={translate('ReleaseFilterProfiles')}>
            <div className={styles.cardGrid}>
              {/* eslint-disable react/jsx-no-bind */}
              {sortedItems.map((item) => (
                <ProfileCard
                  key={item.id}
                  profile={item}
                  onEditPress={() => openEditModal(item)}
                  onDeletePress={() => setDeleteTarget(item)}
                />
              ))}

              <AddCard onPress={openAddModal} />
              {/* eslint-enable react/jsx-no-bind */}
            </div>
          </FieldSet>
        )}

        <EditProfileModal
          draft={draft}
          isSaving={isSaving}
          saveError={saveError}
          setDraft={setDraft}
          onSavePress={saveDraft}
          onModalClose={closeModal}
        />

        <ConfirmModal
          isOpen={!!deleteTarget}
          kind={kinds.DANGER}
          title={translate('Delete')}
          message={translate('DeleteReleaseFilterProfileMessage', { name: deleteTarget?.name })}
          confirmLabel={translate('Delete')}
          isSpinning={isDeleting}
          onConfirm={confirmDelete}
          onCancel={cancelDelete}
        />
      </PageContentBody>
    </PageContent>
  );
}

export default ReleaseFilterSettingsPage;
